using System.Text.RegularExpressions;
using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Domain service that intelligently extracts legal facts and entities from conversational questions,
/// user answers, transcripts, and LLM outputs, formats Bengali numerals and currencies,
/// and stores them into normalized storage for Case Summary generation.
/// </summary>
public sealed class LegalFactExtractorService : ILegalFactExtractorService
{
    private readonly MongoDbContext _context;
    private readonly IConversationAnswerService _answerService;
    private readonly ILogger<LegalFactExtractorService> _logger;

    public LegalFactExtractorService(
        MongoDbContext context,
        IConversationAnswerService answerService,
        ILogger<LegalFactExtractorService> logger)
    {
        _context = context;
        _answerService = answerService;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> ExtractAndPersistFactsAsync(
        Conversation conversation,
        string userText,
        string inputMessageType,
        OpenRouterAnalysisResult? openRouterResult,
        CancellationToken ct = default)
    {
        var extractedFacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------------
        // STEP 1: Determine the specific Question that was asked in the previous turn
        // ---------------------------------------------------------------------
        var previousQuestionText = GetPrecedingQuestionText(conversation);
        var targetFactKey = await ResolveTargetFactKeyAsync(previousQuestionText, conversation.IncidentType, ct);

        _logger.LogInformation("Turn Fact Extraction: PrecedingQuestion='{Question}', TargetFactKey='{Key}'",
            previousQuestionText, targetFactKey);

        // ---------------------------------------------------------------------
        // STEP 2: Extract Value for the Target Question if identified
        // ---------------------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(targetFactKey) && !string.IsNullOrWhiteSpace(userText))
        {
            var cleanAnswer = ExtractCleanAnswerForTarget(targetFactKey, userText);
            if (!string.IsNullOrWhiteSpace(cleanAnswer))
            {
                extractedFacts[targetFactKey] = cleanAnswer;
            }
        }

        // ---------------------------------------------------------------------
        // STEP 3: Omni-Turn Entity Extraction (Names, Amounts, Dates, Evidence, Relief, etc.)
        // ---------------------------------------------------------------------
        ExtractOmniEntitiesFromText(userText, extractedFacts, conversation.IncidentCategoryCode, conversation);

        // ---------------------------------------------------------------------
        // STEP 4: Integrate LLM OpenRouter Extracted Entities
        // ---------------------------------------------------------------------
        if (openRouterResult?.ExtractedInfo != null)
        {
            var info = openRouterResult.ExtractedInfo;

            if (!string.IsNullOrWhiteSpace(info.Applicant) && !IsGenericPlaceholderName(info.Applicant))
            {
                var clean = CleanName(info.Applicant);
                if (!string.IsNullOrWhiteSpace(clean) && !IsGenericPlaceholderName(clean) && !extractedFacts.ContainsKey("applicant"))
                {
                    extractedFacts["applicant"] = clean;
                }
            }

            if (!string.IsNullOrWhiteSpace(info.Respondent) && !IsGenericPlaceholderName(info.Respondent))
            {
                var clean = CleanName(info.Respondent);
                if (!string.IsNullOrWhiteSpace(clean) && !IsGenericPlaceholderName(clean) && !extractedFacts.ContainsKey("respondent"))
                {
                    extractedFacts["respondent"] = clean;
                }
            }

            if (!string.IsNullOrWhiteSpace(info.IncidentDate) && !extractedFacts.ContainsKey("marriageDate") && !extractedFacts.ContainsKey("incidentDate"))
            {
                extractedFacts["incidentDate"] = info.IncidentDate.Trim();
            }

            if (!string.IsNullOrWhiteSpace(info.DisputedAmount) && !extractedFacts.ContainsKey("dowerAmount") && !extractedFacts.ContainsKey("disputedAmount") && !extractedFacts.ContainsKey("unpaidWageAmount"))
            {
                extractedFacts["disputedAmount"] = NormalizeBengaliAmount(info.DisputedAmount);
            }

            if (!string.IsNullOrWhiteSpace(info.Location) && !extractedFacts.ContainsKey("incidentLocation"))
            {
                extractedFacts["incidentLocation"] = info.Location.Trim();
            }

            if (info.EvidenceList is { Count: > 0 })
            {
                var evText = string.Join(", ", info.EvidenceList);
                if (!extractedFacts.ContainsKey("evidenceList"))
                {
                    extractedFacts["evidenceList"] = evText;
                }
            }
        }

        // ---------------------------------------------------------------------
        // STEP 5: Persist extracted facts to conversation_answers and state
        // ---------------------------------------------------------------------
        var sourceLabel = inputMessageType == "Voice" ? "AiIntakeVoice" : "AiIntakeText";

        foreach (var (factKey, factValue) in extractedFacts)
        {
            if (string.IsNullOrWhiteSpace(factValue)) continue;

            // 5a. Save to conversation state dictionary
            conversation.State.InformationAlreadyCollected[factKey] = factValue;

            // 5b. Save to normalized conversation_answers table
            try
            {
                await _answerService.SaveAnswerAsync(new ConversationAnswerModel
                {
                    ConversationId = conversation.Id,
                    CaseId = Guid.TryParse(conversation.CaseId, out var g) ? g : null,
                    QuestionCode = factKey,
                    QuestionText = GetFriendlyQuestionLabel(factKey),
                    Answer = factValue,
                    Source = sourceLabel,
                    Confidence = 1.0,
                    AnsweredAt = DateTime.UtcNow
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to persist answer for key '{Key}'", factKey);
            }
        }

        // ---------------------------------------------------------------------
        // STEP 6: Update conversation.ExtractedData and Analysis.ExtractedData
        // ---------------------------------------------------------------------
        SyncExtractedDataStructure(conversation, extractedFacts);

        _logger.LogInformation("Successfully extracted and persisted {Count} legal facts for conversation {ConvId}",
            extractedFacts.Count, conversation.Id);

        return extractedFacts;
    }

    private static string? GetPrecedingQuestionText(Conversation conversation)
    {
        // 1. Check conversation.State.NextPrompt
        if (!string.IsNullOrWhiteSpace(conversation.State.NextPrompt))
        {
            return conversation.State.NextPrompt.Trim();
        }

        // 2. Check the most recent Assistant message (excluding current user message)
        var lastAssistantMsg = conversation.Messages
            .Where(m => m.Role.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.SequenceNumber)
            .FirstOrDefault();

        return lastAssistantMsg?.Text?.Trim();
    }

    private async Task<string?> ResolveTargetFactKeyAsync(string? questionText, string? incidentType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(questionText)) return null;

        var clean = questionText.Trim();

        // 1. Check MongoDB question_bank for direct match
        try
        {
            var item = await _context.QuestionBank
                .Find(q => q.QuestionBangla == clean && q.IsActive)
                .FirstOrDefaultAsync(ct);

            if (item != null && !string.IsNullOrWhiteSpace(item.RequiredFactKey))
            {
                return item.RequiredFactKey;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "QuestionBank lookup failed for question: {Question}", clean);
        }

        // 2. Domain pattern rules matching canonical questions
        if (clean.Contains("পূর্ণ নাম") && (clean.Contains("আপনার") || clean.Contains("বাদী") || clean.Contains("আবেদনকারী")) && !clean.Contains("স্বামী") && !clean.Contains("মালিক"))
        {
            return "applicant";
        }
        if (clean.Contains("স্বামীর") || clean.Contains("বিবাদী") || clean.Contains("অভিযুক্ত") || clean.Contains("নির্যাতনকারী"))
        {
            return "respondent";
        }
        if (clean.Contains("বিবাহের") && (clean.Contains("তারিখ") || clean.Contains("সাল") || clean.Contains("কবে")))
        {
            return "marriageDate";
        }
        if (clean.Contains("কাবিননামা") && clean.Contains("দেনমোহর") && (clean.Contains("মোট") || clean.Contains("ধার্যকৃত")))
        {
            return "dowerAmount";
        }
        if (clean.Contains("পরিশোধ") && clean.Contains("দেনমোহর"))
        {
            return "paidDowerAmount";
        }
        if (clean.Contains("বকেয়া দেনমোহর") || (clean.Contains("বকেয়া") && clean.Contains("দেনমোহরের")))
        {
            return "unpaidDowerAmount";
        }
        if (clean.Contains("ভরণপোষণ") || clean.Contains("খোরপোষ"))
        {
            return "maintenanceDue";
        }
        if (clean.Contains("সন্তান") && (clean.Contains("সংখ্যা") || clean.Contains("বয়স") || clean.Contains("আছে কি")))
        {
            return "childCount";
        }
        if (clean.Contains("যৌতুক") && clean.Contains("দাবি"))
        {
            return "dowryDemandAmount";
        }
        if (clean.Contains("যৌতুক") && clean.Contains("প্রদান"))
        {
            return "dowryPaidAmount";
        }
        if (clean.Contains("শারীরিক") || clean.Contains("মানসিক") || clean.Contains("নির্যাতন") || clean.Contains("মারধর"))
        {
            return "abuseType";
        }
        if (clean.Contains("ডাক্তারি") || clean.Contains("প্রেসক্রিপশন") || clean.Contains("চিকিৎসা") || clean.Contains("আঘাত"))
        {
            return "injuryDetails";
        }
        if (clean.Contains("মালিক") || clean.Contains("প্রতিষ্ঠান") || clean.Contains("কোম্পানি"))
        {
            return "employerName";
        }
        if (clean.Contains("পদবী") || clean.Contains("কী কাজ"))
        {
            return "jobDesignation";
        }
        if (clean.Contains("মূল বেতন") || clean.Contains("মাসিক বেতন") || clean.Contains("দৈনিক মজুরি"))
        {
            return "monthlySalary";
        }
        if (clean.Contains("কত মাস") || clean.Contains("কত দিনের") || clean.Contains("বকেয়া রয়েছে"))
        {
            return "unpaidMonths";
        }
        if (clean.Contains("মোট কত টাকা মজুরি") || clean.Contains("মজুরি ও পাওনা"))
        {
            return "unpaidWageAmount";
        }
        if (clean.Contains("ওভারটাইম"))
        {
            return "overtimeDue";
        }
        if (clean.Contains("জমি") || clean.Contains("তফসিল") || clean.Contains("মৌজা") || clean.Contains("খতিয়ান") || clean.Contains("দাগ"))
        {
            return "propertyDescription";
        }
        if (clean.Contains("মালিকানা") || clean.Contains("পৈতৃক") || clean.Contains("ক্রয়কৃত") || clean.Contains("হেবা"))
        {
            return "ownershipType";
        }
        if (clean.Contains("বেদখল") || clean.Contains("বঞ্চিত"))
        {
            return "deprivationNature";
        }
        if (clean.Contains("হেফাজত") || clean.Contains("দেখা-সাক্ষাত"))
        {
            return "custodyDisputeNature";
        }
        if (clean.Contains("প্রমাণ") || clean.Contains("নথিপত্র") || clean.Contains("কাগজপত্র") || clean.Contains("দলিল"))
        {
            return "evidenceList";
        }
        if (clean.Contains("প্রতিকার") || clean.Contains("সমাধান") || clean.Contains("কী চান"))
        {
            return "desiredRelief";
        }
        if (clean.Contains("জিডি") || clean.Contains("পুলিশ") || clean.Contains("থানায়"))
        {
            return "policeReport";
        }
        if (clean.Contains("ঘটনাস্থল") || clean.Contains("কোথায় ঘটেছে"))
        {
            return "incidentLocation";
        }

        return null;
    }

    private static string ExtractCleanAnswerForTarget(string targetKey, string userText)
    {
        var trimmed = userText.Trim();

        switch (targetKey)
        {
            case "applicant":
            case "respondent":
            case "employerName":
                var name = CleanName(trimmed);
                if (IsStopWordName(name) || IsGenericPlaceholderName(name)) return string.Empty;
                return name;

            case "dowerAmount":
                if (trimmed.Contains("পরিশোধ") || trimmed.Contains("উসুল")) return string.Empty;
                var amt1 = NormalizeBengaliAmount(trimmed);
                return (amt1.Contains("টাকা") || amt1.Contains("লাখ") || amt1.Contains("হাজার") || Regex.IsMatch(amt1, @"[০-৯0-9]")) ? amt1 : string.Empty;

            case "paidDowerAmount":
                var amt2 = NormalizeBengaliAmount(trimmed);
                return (amt2.Contains("টাকা") || amt2.Contains("লাখ") || amt2.Contains("হাজার") || Regex.IsMatch(amt2, @"[০-৯0-9]")) ? amt2 : string.Empty;

            case "unpaidDowerAmount":
                if (trimmed.Contains("পরিশোধ") || trimmed.Contains("উসুল")) return string.Empty;
                var amt3 = NormalizeBengaliAmount(trimmed);
                return (amt3.Contains("টাকা") || amt3.Contains("লাখ") || amt3.Contains("হাজার") || Regex.IsMatch(amt3, @"[০-৯0-9]")) ? amt3 : string.Empty;

            case "monthlySalary":
            case "unpaidWageAmount":
            case "overtimeDue":
            case "dowryDemandAmount":
            case "dowryPaidAmount":
                var amtGen = NormalizeBengaliAmount(trimmed);
                if (amtGen.Contains("টাকা") || amtGen.Contains("লাখ") || amtGen.Contains("হাজার") || Regex.IsMatch(amtGen, @"[০-৯0-9]"))
                {
                    return amtGen;
                }
                return string.Empty;

            case "maintenanceDue":
                if (trimmed.Contains("মাস") || trimmed.Contains("বছর") || trimmed.Contains("টাকা") || trimmed.Contains("ভরণপোষণ") || trimmed.Contains("খোরপোষ"))
                {
                    var months = ExtractUnpaidMonths(trimmed);
                    return !string.IsNullOrWhiteSpace(months) ? $"{months} বকেয়া" : trimmed;
                }
                return string.Empty;

            case "marriageDate":
            case "incidentDate":
                return ExtractDateExpression(trimmed);

            case "childCount":
            case "childDetails":
                return ExtractChildDetails(trimmed);

            case "evidenceList":
                return ExtractEvidenceDetails(trimmed);

            case "desiredRelief":
                return ExtractReliefDetails(trimmed);

            case "unpaidMonths":
                return ExtractUnpaidMonths(trimmed);

            default:
                return trimmed;
        }
    }

    private static void ExtractOmniEntitiesFromText(string text, Dictionary<string, string> facts, string? categoryCode, Conversation conversation)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // 1. Applicant Name extraction ("আমার নাম ...", "আমার পূর্ণ নাম ...")
        var applicantMatch = Regex.Match(text, @"(?:আমার\s+(?:পূর্ণ\s+|পুরো\s+)?নাম|নাম\s+হলো|নাম\s+হচ্ছে)\s*(?:হলো|হচ্ছে|ছিল|হলেন|:|-)?\s*([^\d,\.।\n\r]{2,35})", RegexOptions.IgnoreCase);
        if (applicantMatch.Success && !facts.ContainsKey("applicant"))
        {
            var name = CleanName(applicantMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(name) && !IsStopWordName(name) && !IsGenericPlaceholderName(name))
            {
                facts["applicant"] = name;
            }
        }

        // 2. Respondent / Husband Name extraction ("স্বামীর নাম ...", "স্বামীর পুরো নাম ...", "বিবাদীর নাম ...")
        var respondentMatch = Regex.Match(text, @"(?:স্বামীর\s+(?:পুরো\s+নাম|পূর্ণ\s+নাম|নাম)|বিবাদীর\s+নাম|অভিযুক্তের\s+নাম)\s*(?:হলো|হচ্ছে|ছিল|হলেন|:|-)?\s*([^\d,\.।\n\r]{2,35})", RegexOptions.IgnoreCase);
        if (respondentMatch.Success && !facts.ContainsKey("respondent"))
        {
            var name = CleanName(respondentMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(name) && !IsStopWordName(name) && !IsGenericPlaceholderName(name))
            {
                facts["respondent"] = name;
            }
        }

        // 3. Employer Name extraction ("মালিক ...", "কোম্পানির নাম ...", "অ্যাপেক্স টেক্সটাইলে ...")
        if (categoryCode == "LABOUR_WAGE")
        {
            var employerMatch = Regex.Match(text, @"(?:কোম্পানি(?:র)?\s+নাম|মালিক(?:ের)?\s+নাম|প্রতিষ্ঠান(?:ের)?\s+নাম)\s*(?:হলো|হচ্ছে|ছিল)?\s*([^\d,\.।\n\r]{2,35})", RegexOptions.IgnoreCase);
            if (employerMatch.Success && !facts.ContainsKey("employerName"))
            {
                var emp = CleanName(employerMatch.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(emp) && !IsGenericPlaceholderName(emp))
                {
                    facts["employerName"] = emp;
                }
            }
            else if (!facts.ContainsKey("employerName"))
            {
                var directEmpMatch = Regex.Match(text, @"([^\s\d,\.।\n\r]{2,25}\s*(?:টেক্সটাইল|গার্মেন্টস|লিমিটেড|ফ্যাক্টরি|কোম্পানি|এন্টারপ্রাইজ|ইন্ডাস্ট্রিজ))", RegexOptions.IgnoreCase);
                if (directEmpMatch.Success)
                {
                    facts["employerName"] = CleanName(directEmpMatch.Groups[1].Value);
                }
            }
        }

        // 3b. Job Designation
        var designationMatch = Regex.Match(text, @"([^\s\d,\.।\n\r]{2,25})\s*(?:হিসেবে|হিসাবে|পদে|পদে\s*চাকরি|পদে\s*কাজ)", RegexOptions.IgnoreCase);
        if (designationMatch.Success && !facts.ContainsKey("jobDesignation"))
        {
            var des = designationMatch.Groups[1].Value.Trim();
            if (!des.Contains("প্রমাণ") && !des.Contains("টাকা"))
            {
                facts["jobDesignation"] = des;
            }
        }

        // 4. Marriage date / Incident date
        var dateMatch = Regex.Match(text, @"(২০\d{2}|20\d{2}|১৯\d{2}|19\d{2})(?:\s*সালের|\s*সালে|\s*বছর)?(?:\s*([^\d,\.।\n\r]{2,20}))?", RegexOptions.IgnoreCase);
        if (dateMatch.Success && !facts.ContainsKey("marriageDate") && !facts.ContainsKey("incidentDate"))
        {
            facts["incidentDate"] = dateMatch.Value.Trim();
            if (categoryCode == "DOWER_MAINTENANCE")
            {
                facts["marriageDate"] = dateMatch.Value.Trim();
            }
        }

        // 4b. Unpaid Months
        var monthMatch = Regex.Match(text, @"([০-৯0-9]+)\s*(?:মাস|মাসের)(?:\s*ধরে|\s*বেতন|\s*বকেয়া)?", RegexOptions.IgnoreCase);
        if (monthMatch.Success && !facts.ContainsKey("unpaidMonths"))
        {
            facts["unpaidMonths"] = $"{monthMatch.Groups[1].Value} মাস";
        }

        // 4c. Maintenance Due extraction
        var maintMatch = Regex.Match(text, @"(?:স্বামী\s*)?(?:গত\s*)?([০-৯0-9]+)\s*(?:মাস|মাসের|দিন|দিনের|বছর|বছরের)\s*(?:ধরে\s*)?(?:কোনো\s*)?(?:ভরণপোষণ|খোরপোষ|খরচ)(?:\s*দিচ্ছে\s*না|\s*দেয়\s*না|\s*বকেয়া|\s*দেওয়া\s*হয়নি)?", RegexOptions.IgnoreCase);
        if (maintMatch.Success && !facts.ContainsKey("maintenanceDue"))
        {
            facts["maintenanceDue"] = $"{maintMatch.Groups[1].Value} মাসের বকেয়া ভরণপোষণ";
        }

        // 5. Amounts & Financial figures
        ExtractAmountsFromText(text, facts, categoryCode, conversation);

        // 6. Evidence extraction
        var evidence = ExtractEvidenceDetails(text);
        if (!string.IsNullOrWhiteSpace(evidence) && !facts.ContainsKey("evidenceList"))
        {
            facts["evidenceList"] = evidence;
        }

        // 7. Relief extraction
        var relief = ExtractReliefDetails(text);
        if (!string.IsNullOrWhiteSpace(relief) && !facts.ContainsKey("desiredRelief"))
        {
            facts["desiredRelief"] = relief;
        }

        // 8. Child Details
        var child = ExtractChildDetails(text);
        if (!string.IsNullOrWhiteSpace(child) && !facts.ContainsKey("childCount"))
        {
            facts["childCount"] = child;
        }

        // 9. Abuse & Injuries
        if (text.Contains("মারধর") || text.Contains("নির্যাতন") || text.Contains("গালাগালি") || text.Contains("হুমকি"))
        {
            if (!facts.ContainsKey("abuseType"))
            {
                facts["abuseType"] = "শারীরিক ও মানসিক নির্যাতন";
            }
        }
        if (text.Contains("হাসপাতাল") || text.Contains("ডাক্তার") || text.Contains("চিকিৎসা") || text.Contains("প্রেসক্রিপশন") || text.Contains("আঘাত"))
        {
            if (!facts.ContainsKey("injuryDetails"))
            {
                facts["injuryDetails"] = "শারীরিক আঘাত ও চিকিৎসা গ্রহণ করা হয়েছে";
            }
        }

        // 10. Land & Property specifics
        if (text.Contains("পৈতৃক") || text.Contains("উত্তরাধিকার") || text.Contains("ক্রয়কৃত") || text.Contains("হেবা") || text.Contains("দানপত্র") || text.Contains("দেনমোহর সূত্রে"))
        {
            if (!facts.ContainsKey("ownershipType"))
            {
                if (text.Contains("পৈতৃক") || text.Contains("উত্তরাধিকার")) facts["ownershipType"] = "পৈতৃক / উত্তরাধিকার সূত্রে প্রাপ্ত";
                else if (text.Contains("ক্রয়কৃত") || text.Contains("কেনার")) facts["ownershipType"] = "ক্রয়কৃত সূত্রে";
                else if (text.Contains("হেবা")) facts["ownershipType"] = "হেবা সূত্রে প্রাপ্ত";
                else if (text.Contains("দেনমোহর")) facts["ownershipType"] = "দেনমোহর সূত্রে প্রাপ্ত";
                else facts["ownershipType"] = "আইনগত স্বত্বাধিকার";
            }
        }

        if (text.Contains("মৌজা") || text.Contains("খতিয়ান") || text.Contains("দাগ") || text.Contains("শতক") || text.Contains("কাঠা") || text.Contains("বিঘা") || text.Contains("একর"))
        {
            if (!facts.ContainsKey("propertyDescription"))
            {
                var cleanDesc = Regex.Replace(text, @"(?:আমার|বাদী|বিবাদী|নাম|দলিল|ও|পরচা|আছে|চাই).*$", "").Trim();
                facts["propertyDescription"] = !string.IsNullOrWhiteSpace(cleanDesc) ? cleanDesc : text.Trim();
            }
        }

        if (text.Contains("বেদখল") || text.Contains("দখল") || text.Contains("জবরদখল") || text.Contains("ভাগ দিচ্ছে না") || text.Contains("অংশ দিচ্ছে না"))
        {
            if (!facts.ContainsKey("deprivationNature"))
            {
                if (text.Contains("বেদখল") || text.Contains("দখল করে")) facts["deprivationNature"] = "সম্পত্তি অবৈধভাবে জবরদখল / বেদখল";
                else if (text.Contains("ভাগ দিচ্ছে না") || text.Contains("অংশ দিচ্ছে না")) facts["deprivationNature"] = "উত্তরাধিকারের প্রাপ্য অংশ প্রদানে অস্বীকৃতি";
                else facts["deprivationNature"] = "আইনসম্মত অধিকার থেকে বঞ্চিত করা";
            }
        }
    }

    private static void ExtractAmountsFromText(string text, Dictionary<string, string> facts, string? categoryCode, Conversation conversation)
    {
        // 1. Total dower amount: "দেনমোহর ধার্য ছিল ৫ লাখ টাকা", "কাবিন ৫ লাখ"
        var dowerMatch = Regex.Match(text, @"(?:দেনমোহর|কাবিন)(?:নামা)?(?:\s*ছিল|\s*ধার্য)?\s*(?:ছিল\s*)?([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)", RegexOptions.IgnoreCase);
        if (!dowerMatch.Success)
        {
            dowerMatch = Regex.Match(text, @"(?:দেনমোহর|কাবিন)\s*(?:ছিল|ধার্য)?\s*([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার))", RegexOptions.IgnoreCase);
        }
        if (dowerMatch.Success && !facts.ContainsKey("dowerAmount"))
        {
            var amt = NormalizeBengaliAmount(dowerMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt)) facts["dowerAmount"] = amt;
        }

        // 2. Paid dower amount: "৫০ হাজার টাকা উসুল", "উসুল ৫০ হাজার", "৫০ হাজার টাকা দেনমোহর পরিশোধ করেছিল"
        var paidMatch = Regex.Match(text, @"([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)?\s*(?:দেনমোহর\s*)?(?:পরিশোধ|উসুল|দেওয়া|দিয়েছে|দিছে)(?:\s*(?:ছিল|করেছিল|হয়েছে))?", RegexOptions.IgnoreCase);
        if (!paidMatch.Success || string.IsNullOrWhiteSpace(paidMatch.Groups[1].Value))
        {
            paidMatch = Regex.Match(text, @"(?:পরিশোধ|উসুল|দেওয়া\s*হয়েছে|দিছে|দিয়েছে|দিয়েছিল)\s*(?:ছিল\s*)?([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)?", RegexOptions.IgnoreCase);
        }
        if (paidMatch.Success && !facts.ContainsKey("paidDowerAmount"))
        {
            var matchStr = !string.IsNullOrWhiteSpace(paidMatch.Groups[1].Value) ? paidMatch.Groups[1].Value : paidMatch.Value;
            var amt = NormalizeBengaliAmount(matchStr);
            if (!string.IsNullOrWhiteSpace(amt)) facts["paidDowerAmount"] = amt;
        }

        // 3. Due / Unpaid dower or wage amount
        var wageDueMatch = Regex.Match(text, @"(?:বকেয়া\s*মজুরি|বকেয়া\s*বেতন|মোট\s*বকেয়া\s*মজুরি|মোট\s*বকেয়া)\s*(?:ছিল|হলো|পাওনা)?\s*([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)?", RegexOptions.IgnoreCase);
        if (wageDueMatch.Success && !string.IsNullOrWhiteSpace(wageDueMatch.Groups[1].Value) && !facts.ContainsKey("unpaidWageAmount"))
        {
            var amt = NormalizeBengaliAmount(wageDueMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt)) facts["unpaidWageAmount"] = amt;
        }

        var dowerDueMatch = Regex.Match(text, @"(?:বকেয়া\s*দেনমোহর|পাওনা\s*দেনমোহর|বকেয়া)\s*(?:ছিল|হলো)?\s*([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)?", RegexOptions.IgnoreCase);
        if (dowerDueMatch.Success && !string.IsNullOrWhiteSpace(dowerDueMatch.Groups[1].Value))
        {
            var amt = NormalizeBengaliAmount(dowerDueMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt))
            {
                if (categoryCode == "LABOUR_WAGE" && !facts.ContainsKey("unpaidWageAmount"))
                {
                    facts["unpaidWageAmount"] = amt;
                }
                else if (!facts.ContainsKey("unpaidDowerAmount"))
                {
                    facts["unpaidDowerAmount"] = amt;
                }
            }
        }

        // 4. Overtime due
        var overtimeMatch = Regex.Match(text, @"(?:বকেয়া\s*)?(?:ওভারটাইম|ওভার\s*টাইম)(?:\s*ভাতা)?\s*(?:ছিল|হলো|পাওনা)?\s*([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)?", RegexOptions.IgnoreCase);
        if (overtimeMatch.Success && !string.IsNullOrWhiteSpace(overtimeMatch.Groups[1].Value) && !facts.ContainsKey("overtimeDue"))
        {
            var amt = NormalizeBengaliAmount(overtimeMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt)) facts["overtimeDue"] = amt;
        }

        // 5. Monthly salary
        var salaryMatch = Regex.Match(text, @"(?:মূল\s*বেতন|মাসিক\s*বেতন|বেতন|মজুরি)\s*(?:ছিল|হলো)?\s*([০-৯0-9\s,]+(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)?", RegexOptions.IgnoreCase);
        if (salaryMatch.Success && !string.IsNullOrWhiteSpace(salaryMatch.Groups[1].Value) && !facts.ContainsKey("monthlySalary"))
        {
            var amt = NormalizeBengaliAmount(salaryMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt)) facts["monthlySalary"] = amt;
        }

        // 6. Calculate unpaid dower automatically if total and paid are known but unpaid is not explicitly stated
        var totalDowerStr = facts.TryGetValue("dowerAmount", out var td) ? td : (conversation?.State?.InformationAlreadyCollected.TryGetValue("dowerAmount", out var td2) == true ? td2 : null);
        var paidDowerStr = facts.TryGetValue("paidDowerAmount", out var pd) ? pd : (conversation?.State?.InformationAlreadyCollected.TryGetValue("paidDowerAmount", out var pd2) == true ? pd2 : null);

        if (!string.IsNullOrWhiteSpace(totalDowerStr) && !string.IsNullOrWhiteSpace(paidDowerStr) && !facts.ContainsKey("unpaidDowerAmount"))
        {
            var totalVal = ParseAmountToDouble(totalDowerStr);
            var paidVal = ParseAmountToDouble(paidDowerStr);
            if (totalVal > 0 && paidVal >= 0 && totalVal >= paidVal)
            {
                var unpaidVal = totalVal - paidVal;
                facts["unpaidDowerAmount"] = ConvertEnglishToBengaliDigits(((long)unpaidVal).ToString("N0")) + " টাকা";
            }
        }

        // 7. Dowry demand
        var dowryMatch = Regex.Match(text, @"(?:যৌতুক|দাবি)\s*(?:করেছে)?\s*([০-৯0-9\s,]+(?:লাখ|লক্ষ|হাজার)?\s*টাকা)?", RegexOptions.IgnoreCase);
        if (dowryMatch.Success && !string.IsNullOrWhiteSpace(dowryMatch.Groups[1].Value) && !facts.ContainsKey("dowryDemandAmount"))
        {
            var amt = NormalizeBengaliAmount(dowryMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt)) facts["dowryDemandAmount"] = amt;
        }

        // 8. Generic fallback amount
        var genericAmountMatch = Regex.Match(text, @"([০-৯0-9]+(?:\s*,\s*[০-৯0-9]+)*\s*(?:লাখ|লক্ষ|কোটি|হাজার)?\s*টাকা)", RegexOptions.IgnoreCase);
        if (genericAmountMatch.Success && !facts.ContainsKey("disputedAmount") && !facts.ContainsKey("dowerAmount") && !facts.ContainsKey("unpaidWageAmount"))
        {
            var amt = NormalizeBengaliAmount(genericAmountMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(amt)) facts["disputedAmount"] = amt;
        }
    }

    public static string NormalizeBengaliAmount(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var clean = input.Trim();

        var hasLakh = clean.Contains("লাখ") || clean.Contains("লক্ষ") || clean.Contains("lakh", StringComparison.OrdinalIgnoreCase);
        var hasThousand = clean.Contains("হাজার") || clean.Contains("k", StringComparison.OrdinalIgnoreCase);
        var hasCrore = clean.Contains("কোটি") || clean.Contains("crore", StringComparison.OrdinalIgnoreCase);

        var digitMatch = Regex.Match(clean, @"[০-৯0-9,]+(?:\.[০-৯0-9]+)?");
        if (digitMatch.Success)
        {
            var numStr = ConvertBengaliToEnglishDigits(digitMatch.Value).Replace(",", "").Trim();
            if (double.TryParse(numStr, out var num))
            {
                if (hasCrore) num *= 10000000;
                else if (hasLakh) num *= 100000;
                else if (hasThousand && num < 1000) num *= 1000;

                var formattedBangla = ConvertEnglishToBengaliDigits(((long)num).ToString("N0")) + " টাকা";
                return formattedBangla;
            }
        }

        if (!clean.Contains("টাকা") && (clean.Contains("লাখ") || clean.Contains("হাজার") || clean.Contains("লক্ষ")))
        {
            return clean + " টাকা";
        }

        return clean;
    }

    public static double ParseAmountToDouble(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;
        var clean = input.Trim();
        var hasLakh = clean.Contains("লাখ") || clean.Contains("লক্ষ");
        var hasThousand = clean.Contains("হাজার");
        var hasCrore = clean.Contains("কোটি");

        var digitMatch = Regex.Match(clean, @"[০-৯0-9,]+(?:\.[০-৯0-9]+)?");
        if (digitMatch.Success)
        {
            var raw = ConvertBengaliToEnglishDigits(digitMatch.Value).Replace(",", "");
            if (double.TryParse(raw, out var val))
            {
                if (hasCrore && val < 10000000) val *= 10000000;
                else if (hasLakh && val < 100000) val *= 100000;
                else if (hasThousand && val < 1000) val *= 1000;
                return val;
            }
        }
        return 0;
    }

    private static string CleanName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var clean = input.Trim();

        // Strip prefixes
        clean = Regex.Replace(clean, @"^(?:আমার\s+পূর্ণ\s+নাম|আমার\s+পুরো\s+নাম|আমার\s+নাম|নাম\s+হলো|নাম\s+হচ্ছে|নাম|স্বামীর\s+পুরো\s+নাম|স্বামীর\s+পূর্ণ\s+নাম|স্বামীর\s+নাম|স্বামী\s+হলেন|স্বামী\s+হচ্ছে|স্বামী\s+হলো|স্বামী|বিবাদী|অভিযুক্ত|মালিক(?:ের)?\s+নাম|কোম্পানি(?:র)?\s+নাম)\s*(?:হলো|হচ্ছে|ছিল|হলেন|:|-)?\s*", "", RegexOptions.IgnoreCase);

        // Strip clauses starting with father's name, workplace, address, age, etc.
        clean = Regex.Replace(clean, @"(?:\s*,\s*|\s+)(?:তার\s+)?(?:পিতা|পিতার\s+নাম|বাবার\s+নাম|বাবা|ঠিকানা|কর্মস্থল|গ্রাম|জেলা|থানা|বয়স|মোবাইল|ফোন).*$", "", RegexOptions.IgnoreCase);

        // Strip trailing words / clauses after the name
        clean = Regex.Replace(clean, @"\s+(?:আমাকে|আমার|এবং|ও|যার|যিনি|টাকা|কাবিন|দেনমোহর|বাড়ি|বাড়ি|মালিক|বিরুদ্ধে|কাছে|দ্বারা|করে|দিচ্ছে|মারধর|হয়ে).*$", "", RegexOptions.IgnoreCase);

        // Strip trailing punctuation
        clean = clean.Trim('.', ',', '।', '?', '!', ';', ':', ' ');

        return clean;
    }

    private static string ExtractDateExpression(string input)
    {
        var clean = input.Trim();
        var yearMatch = Regex.Match(clean, @"(২০\d{2}|20\d{2}|১৯\d{2}|19\d{2})");
        if (yearMatch.Success)
        {
            return clean;
        }

        if (clean.Contains("বছর আগে") || clean.Contains("মাস আগে") || clean.Contains("সাল"))
        {
            return clean;
        }

        return clean;
    }

    private static string ExtractChildDetails(string input)
    {
        var clean = input.Trim();
        if (clean.Contains("নাই") || clean.Contains("নেই") || (clean.Contains("না") && !clean.Contains("নাম")))
        {
            if (clean.Contains("সন্তান") || clean.Contains("বাচ্চা") || clean.Contains("ছেলে") || clean.Contains("মেয়ে"))
            {
                return "কোনো সন্তান নেই";
            }
        }

        if (clean.Contains("সন্তান") || clean.Contains("ছেলে") || clean.Contains("মেয়ে") || clean.Contains("বাচ্চা"))
        {
            var childDigitMatch = Regex.Match(clean, @"([০-৯0-9]+)\s*(?:জন|টি)?\s*(?:সন্তান|বাচ্চা|ছেলে|মেয়ে)?");
            if (childDigitMatch.Success && !string.IsNullOrWhiteSpace(childDigitMatch.Groups[1].Value))
            {
                return $"{childDigitMatch.Groups[1].Value} সন্তান";
            }
            if (clean.Contains("সাথে আছে") || clean.Contains("আমার কাছে") || clean.Contains("আমার সাথে"))
            {
                return "সন্তান সাথে রয়েছে";
            }
            return clean;
        }

        return string.Empty;
    }

    private static string ExtractEvidenceDetails(string input)
    {
        var clean = input.Trim();
        var evidenceList = new List<string>();

        if (clean.Contains("কাবিননামা") || clean.Contains("নিকাহনামা") || clean.Contains("কাবিন"))
            evidenceList.Add("কাবিননামা");
        if (clean.Contains("শালিসনামা") || clean.Contains("মীমাংসা"))
            evidenceList.Add("শালিসনামা");
        if (clean.Contains("পে-স্লিপ") || clean.Contains("বেতনের রসিদ") || clean.Contains("স্লিপ"))
            evidenceList.Add("পে-স্লিপ");
        if (clean.Contains("আইডি কার্ড") || clean.Contains("পরিচয়পত্র"))
            evidenceList.Add("আইডি কার্ড");
        if (clean.Contains("হাজিরা খাতা") || clean.Contains("হাজিরা"))
            evidenceList.Add("হাজিরা খাতা");
        if (clean.Contains("নিয়োগপত্র") || clean.Contains("অফার লেটার"))
            evidenceList.Add("নিয়োগপত্র");
        if (clean.Contains("খতিয়ান") || clean.Contains("পরচা"))
            evidenceList.Add("খতিয়ান/পরচা");
        if (clean.Contains("দলিল"))
            evidenceList.Add("জমির দলিল");
        if (clean.Contains("মেডিকেল") || clean.Contains("ডাক্তার") || clean.Contains("প্রেসক্রিপশন") || clean.Contains("সার্টিফিকেট"))
            evidenceList.Add("ডাক্তারি প্রেসক্রিপশন/সার্টিফিকেট");
        if (clean.Contains("জিডি") || clean.Contains("অভিযোগ"))
            evidenceList.Add("থানার জিডি/অভিযোগপত্র");
        if (clean.Contains("ছবি") || clean.Contains("ফটোগ্রাফ"))
            evidenceList.Add("ছবি/ফটোগ্রাফিক প্রমাণ");
        if (clean.Contains("অডিও") || clean.Contains("কল রেকর্ড"))
            evidenceList.Add("অডিও কল রেকর্ড");

        if (evidenceList.Count > 0)
        {
            return string.Join(", ", evidenceList);
        }

        if (clean.Contains("কাগজপত্র") || clean.Contains("দলিলপত্র") || clean.Contains("প্রমাণাদি") || clean.Contains("নথিপত্র"))
        {
            return clean;
        }

        return string.Empty;
    }

    private static string ExtractReliefDetails(string input)
    {
        var clean = input.Trim();
        var reliefs = new List<string>();

        if (clean.Contains("দেনমোহর")) reliefs.Add("বকেয়া দেনমোহর আদায়");
        if (clean.Contains("খোরপোষ") || clean.Contains("ভরণপোষণ")) reliefs.Add("ভরণপোষণ আদায়");
        if (clean.Contains("বেতন") || clean.Contains("মজুরি")) reliefs.Add("বকেয়া মজুরি ও সার্ভিস বেনিফিট আদায়");
        if (clean.Contains("নিরাপত্তা") || clean.Contains("সুরক্ষা")) reliefs.Add("আইনি নিরাপত্তা ও সুরক্ষা আদেশ");
        if (clean.Contains("হেফাজত") || clean.Contains("সন্তান ফেরত")) reliefs.Add("সন্তানের হেফাজত ও দেখা-সাক্ষাতের অধিকার");
        if (clean.Contains("জমি") || clean.Contains("সম্পত্তি")) reliefs.Add("সম্পত্তি পুনরুদ্ধার ও স্বত্ব সাব্যস্ত");
        if (clean.Contains("মামলা") || clean.Contains("আইনি ব্যবস্থা") || clean.Contains("বিচার")) reliefs.Add("আইনি প্রতিকার ও বিচার প্রার্থনা");

        if (reliefs.Count > 0)
        {
            return string.Join(" এবং ", reliefs);
        }

        if (clean.Contains("আদায়") || clean.Contains("চাই") || clean.Contains("প্রতিকার") || clean.Contains("সহায়তা") || clean.Contains("সাহায্য"))
        {
            return clean;
        }

        return string.Empty;
    }

    private static string ExtractUnpaidMonths(string input)
    {
        var clean = input.Trim();
        var monthMatch = Regex.Match(clean, @"([০-৯0-9]+)\s*(?:মাস|মাসের)");
        if (monthMatch.Success)
        {
            return $"{monthMatch.Groups[1].Value} মাস";
        }
        return clean;
    }

    private static bool IsStopWordName(string name)
    {
        var stops = new[] { "হ্যাঁ", "না", "ঠিক আছে", "জি", "হাঁ", "দেনমোহর", "টাকা", "মামলা", "জানি না", "বলছি", "আচ্ছা", "এখন", "গত", "আমার", "আমাদের", "বাড়ি", "বাড়ি", "থাকি", "আছি", "কখনো", "কখনই", "নাগরিক", "ভুক্তভোগী", "সন্তান" };
        return stops.Any(s => name.Equals(s, StringComparison.OrdinalIgnoreCase) || name.Length < 2);
    }

    private static string ConvertBengaliToEnglishDigits(string input)
    {
        return input
            .Replace('০', '0')
            .Replace('১', '1')
            .Replace('২', '2')
            .Replace('৩', '3')
            .Replace('৪', '4')
            .Replace('৫', '5')
            .Replace('৬', '6')
            .Replace('৭', '7')
            .Replace('৮', '8')
            .Replace('৯', '9');
    }

    private static string ConvertEnglishToBengaliDigits(string input)
    {
        return input
            .Replace('0', '০')
            .Replace('1', '১')
            .Replace('2', '২')
            .Replace('3', '৩')
            .Replace('4', '৪')
            .Replace('5', '৫')
            .Replace('6', '৬')
            .Replace('7', '৭')
            .Replace('8', '৮')
            .Replace('9', '৯');
    }

    private static string GetFriendlyQuestionLabel(string factKey)
    {
        return factKey switch
        {
            "applicant" => "আবেদনকারীর নাম / ভুক্তভোগী",
            "respondent" => "বিবাদী / প্রতিপক্ষের নাম",
            "marriageDate" => "বিবাহের তারিখ ও সময়কাল",
            "dowerAmount" => "দেনমোহরের মোট ধার্যকৃত অঙ্ক",
            "paidDowerAmount" => "পরিশোধিত দেনমোহর",
            "unpaidDowerAmount" => "বকেয়া দেনমোহরের পরিমাণ",
            "maintenanceDue" => "বকেয়া ভরণপোষণ দাবি",
            "childCount" => "সন্তানের সংখ্যা ও ভরণপোষণ",
            "dowryDemandAmount" => "দাবিকৃত যৌতুকের বিবরণ ও অঙ্ক",
            "dowryPaidAmount" => "পূর্বে প্রদত্ত যৌতুক",
            "abuseType" => "শারীরিক / মানসিক নির্যাতনের ধরন",
            "injuryDetails" => "শারীরিক আঘাত ও চিকিৎসা",
            "employerName" => "মালিক / প্রতিষ্ঠানের নাম",
            "jobDesignation" => "পদবী ও কাজের বিবরণ",
            "monthlySalary" => "মাসিক মূল বেতন / মজুরি",
            "unpaidMonths" => "বকেয়া মাসের সংখ্যা",
            "unpaidWageAmount" => "মোট বকেয়া মজুরির দাবি",
            "overtimeDue" => "বকেয়া ওভারটাইম ভাতা",
            "propertyDescription" => "সম্পত্তির তফসিল ও বিবরণ",
            "ownershipType" => "মালিকানার ভিত্তি / সূত্র",
            "deprivationNature" => "বেদখল বা অধিকার হরণের ধরন",
            "childDetails" => "সন্তানদের বিস্তারিত তথ্য",
            "custodyDisputeNature" => "হেফাজত বিরোধ ও বাধার বিবরণ",
            "evidenceList" => "প্রমাণাদি ও নথিপত্র",
            "desiredRelief" => "প্রার্থিত আইনি প্রতিকার",
            "policeReport" => "থানায় জিডি বা অভিযোগ",
            "incidentLocation" => "ঘটনাস্থল",
            _ => factKey
        };
    }

    private static bool IsGenericPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        var placeholders = new[] { "ভুক্তভোগী", "ভুক্তভোগী নাগরিক", "নাগরিক", "বাদী", "আবেদনকারী", "অভিযোগকারী", "বিবাদী", "অভিযুক্ত", "প্রতিপক্ষ", "মালিক", "অজ্ঞাত", "অজ্ঞাতনামা" };
        return placeholders.Any(p => trimmed.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static void SyncExtractedDataStructure(Conversation conversation, Dictionary<string, string> facts)
    {
        var existingApp = conversation.ExtractedData?.Applicant?.Value;
        var newApp = facts.TryGetValue("applicant", out var appVal) ? appVal : null;
        var applicant = (!string.IsNullOrWhiteSpace(newApp) && !IsGenericPlaceholderName(newApp))
            ? newApp
            : (!string.IsNullOrWhiteSpace(existingApp) && !IsGenericPlaceholderName(existingApp) ? existingApp : (newApp ?? existingApp));

        var existingRes = conversation.ExtractedData?.Respondent?.Value;
        var newRes = facts.TryGetValue("respondent", out var resVal) ? resVal : (facts.TryGetValue("employerName", out var empVal) ? empVal : null);
        var respondent = (!string.IsNullOrWhiteSpace(newRes) && !IsGenericPlaceholderName(newRes))
            ? newRes
            : (!string.IsNullOrWhiteSpace(existingRes) && !IsGenericPlaceholderName(existingRes) ? existingRes : (newRes ?? existingRes));

        var dateVal = facts.TryGetValue("marriageDate", out var mDate) ? mDate : (facts.TryGetValue("incidentDate", out var iDate) ? iDate : conversation.ExtractedData?.MarriageOrIncidentDate?.Value);
        var amountVal = facts.TryGetValue("dowerAmount", out var dVal) ? dVal : (facts.TryGetValue("unpaidWageAmount", out var wVal) ? wVal : (facts.TryGetValue("disputedAmount", out var disVal) ? disVal : conversation.ExtractedData?.DisputedAmount?.Value));

        List<string> evidenceList = conversation.ExtractedData?.EvidenceList?.ToList() ?? new List<string>();
        if (facts.TryGetValue("evidenceList", out var evStr) && !string.IsNullOrWhiteSpace(evStr))
        {
            var split = evStr.Split(',', ';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in split)
            {
                if (!evidenceList.Contains(item, StringComparer.OrdinalIgnoreCase))
                {
                    evidenceList.Add(item);
                }
            }
        }

        var newExt = new ExtractedLegalData(
            Applicant: !string.IsNullOrWhiteSpace(applicant) ? new InferredField(applicant, false) : null,
            Respondent: !string.IsNullOrWhiteSpace(respondent) ? new InferredField(respondent, false) : null,
            MarriageOrIncidentDate: !string.IsNullOrWhiteSpace(dateVal) ? new InferredField(dateVal, false) : null,
            DisputedAmount: !string.IsNullOrWhiteSpace(amountVal) ? new InferredField(amountVal, false) : null,
            EvidenceList: evidenceList.Count > 0 ? evidenceList : null
        );

        conversation.ExtractedData = newExt;
        if (conversation.Analysis != null)
        {
            conversation.Analysis.ExtractedData = newExt;
        }
    }
}
