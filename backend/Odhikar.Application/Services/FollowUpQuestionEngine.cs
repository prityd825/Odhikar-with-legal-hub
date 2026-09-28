using Microsoft.Extensions.Options;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Application.Services;

/// <summary>
/// Follow-up Question Engine implementing the 6-step flow with configurable priority rules
/// and integration with existing alarming detection as a high-priority safety signal:
/// 1. Read the latest AI analysis
/// 2. Read the Conversation State
/// 3. Find missing information
/// 4. Check questions already asked
/// 5. Prioritize the most important missing information (using configurable priority rules & alarming signal elevation)
/// 6. Select a suitable question from the Question Bank
///
/// Prevents duplicate or irrelevant questions, and integrates cleanly with ConversationOrchestrator.
/// </summary>
public sealed class FollowUpQuestionEngine(
    IQuestionBankService questionBank,
    IQuestionCategoryService categoryService,
    ISafetyGuardrailService safetyGuardrail,
    IOptions<FollowUpPrioritySettings> priorityOptions,
    ILogger<FollowUpQuestionEngine> logger) : IFollowUpQuestionEngine
{
    private readonly IQuestionBankService _questionBank = questionBank;
    private readonly IQuestionCategoryService _categoryService = categoryService;
    private readonly ISafetyGuardrailService _safetyGuardrail = safetyGuardrail;
    private readonly FollowUpPrioritySettings _prioritySettings = priorityOptions.Value ?? new FollowUpPrioritySettings();
    private readonly ILogger<FollowUpQuestionEngine> _logger = logger;

    // Mapping of legal fact keys to their corresponding category IDs
    private static readonly Dictionary<string, string> FactToCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["applicant"] = "applicant",
        ["respondent"] = "perpetrator",
        ["perpetrator"] = "perpetrator",
        ["employerName"] = "perpetrator",
        ["incident_date"] = "incident_time",
        ["incident_time"] = "incident_time",
        ["marriageDate"] = "incident_time",
        ["marriage_date"] = "incident_time",
        ["incidentDate"] = "incident_time",
        ["unpaidMonths"] = "incident_time",
        ["incident_location"] = "incident_location",
        ["location"] = "incident_location",
        ["incidentLocation"] = "incident_location",
        ["disputed_amount"] = "financial_details",
        ["dower_amount"] = "financial_details",
        ["dowerAmount"] = "financial_details",
        ["paidDowerAmount"] = "financial_details",
        ["unpaidDowerAmount"] = "financial_details",
        ["maintenance_amount"] = "financial_details",
        ["maintenanceDue"] = "financial_details",
        ["childCount"] = "financial_details",
        ["dowry_demand"] = "financial_details",
        ["dowryDemandAmount"] = "financial_details",
        ["dowryPaidAmount"] = "financial_details",
        ["monthlySalary"] = "financial_details",
        ["unpaidWageAmount"] = "financial_details",
        ["overtimeDue"] = "financial_details",
        ["inheritanceShare"] = "financial_details",
        ["economicControl"] = "financial_details",
        ["financialLoss"] = "financial_details",
        ["evidence"] = "evidence_documents",
        ["documents"] = "evidence_documents",
        ["evidenceList"] = "evidence_documents",
        ["nikahnama"] = "evidence_documents",
        ["ownershipType"] = "evidence_documents",
        ["policeReport"] = "evidence_documents",
        ["witnesses"] = "witness_details",
        ["witnessList"] = "witness_details",
        ["localArbitration"] = "witness_details",
        ["current_safety"] = "current_safety",
        ["safety"] = "current_safety",
        ["immediateThreat"] = "current_safety",
        ["childSafetyRisk"] = "current_safety",
        ["threat_details"] = "threat_details",
        ["threats"] = "threat_details",
        ["threatDetails"] = "threat_details",
        ["abuseType"] = "threat_details",
        ["custodyDisputeNature"] = "threat_details",
        ["movementRestriction"] = "threat_details",
        ["injury_details"] = "injury_details",
        ["injuries"] = "injury_details",
        ["injuryDetails"] = "injury_details",
        ["medicalObstruction"] = "injury_details",
        ["desired_relief"] = "desired_relief",
        ["desiredRelief"] = "desired_relief",
        ["incident_description"] = "incident_description",
        ["incidentDescription"] = "incident_description",
        ["key_facts"] = "incident_description",
        ["jobDesignation"] = "incident_description",
        ["terminationDetails"] = "incident_description",
        ["propertyDescription"] = "incident_description",
        ["deprivationNature"] = "incident_description",
        ["childDetails"] = "incident_description",
        ["fraudDetails"] = "incident_description",
        ["clarification"] = "incident_type_clarification"
    };

    public async Task<FollowUpQuestionDecision> DetermineNextQuestionAsync(
        AIAnalysis? latestAnalysis,
        ConversationState state,
        string? incidentType,
        string? userTranscript = null,
        SafetyResult? safetyScanResult = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Follow-up Question Engine running for conversation {ConversationId} (Turn: {Turn})",
            state.ConversationId, state.TurnCount);

        // ---------------------------------------------------------------------
        // STEP 1: Read the latest AI analysis & evaluate Alarming Safety Signals
        // ---------------------------------------------------------------------
        var analysisMissingFields = latestAnalysis?.MissingFields ?? [];
        var isInfoSufficient = latestAnalysis?.IsInformationSufficient ?? false;
        var severity = latestAnalysis?.Severity ?? "Low";
        var isUrgentOrHighSeverity = string.Equals(severity, "High", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(severity, "Critical", StringComparison.OrdinalIgnoreCase);

        // Evaluate existing alarming detection as higher-priority safety signal
        var safetyResult = safetyScanResult;
        if (safetyResult == null && !string.IsNullOrWhiteSpace(userTranscript))
        {
            try
            {
                safetyResult = await _safetyGuardrail.ScanAsync(userTranscript, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Safety guardrail scan failed in FollowUpQuestionEngine; proceeding with keyword heuristic.");
            }
        }

        var combinedText = BuildContextText(userTranscript, state);
        var indicatesDanger = HasDangerSignals(combinedText);
        var isAlarmingDetected = (safetyResult is { IsTriggered: true }) ||
                                 latestAnalysis?.IsSafetyEscalated == true ||
                                 isUrgentOrHighSeverity ||
                                 indicatesDanger;

        if (isAlarmingDetected)
        {
            _logger.LogWarning(
                "🚨 Alarming safety signal detected in Follow-up Question Engine for conversation {ConversationId}. Reason: {Reason}, Keyword: {Keyword}. Elevating safety tiers to highest priority.",
                state.ConversationId,
                safetyResult?.Reason ?? "Distress/Danger keywords detected",
                safetyResult?.MatchedSignal ?? "danger_signal");
        }

        // ---------------------------------------------------------------------
        // STEP 2: Read the Conversation State
        // ---------------------------------------------------------------------
        var collected = state.InformationAlreadyCollected;
        var askedQuestions = state.QuestionsAlreadyAsked;
        var askedCategories = state.AskedQuestionCategories;

        // ---------------------------------------------------------------------
        // STEP 3: Prioritize Question Bank Questions for Active Incident Category
        // ---------------------------------------------------------------------
        var parentCode = ResolveParentCategoryCode(incidentType);
        var parentBankQuestions = await _questionBank.GetByParentCategoryAsync(parentCode, ct);

        _logger.LogInformation("Step 3: Found {Count} Question Bank items for Parent Category '{ParentCode}'",
            parentBankQuestions.Count, parentCode);

        // Find unasked questions whose required fact is not yet collected with real data
        var unaskedParentQuestions = parentBankQuestions
            .Where(q => q.IsActive && !IsDuplicateQuestion(q.QuestionBangla, askedQuestions))
            .Where(q => string.IsNullOrWhiteSpace(q.RequiredFactKey) || !IsFactAlreadyCollected(q.RequiredFactKey, collected))
            .ToList();

        // ---------------------------------------------------------------------
        // STEP 4: Handle Alarming Signals Priority Elevation
        // ---------------------------------------------------------------------
        if (isAlarmingDetected)
        {
            var safetyQuestions = await _questionBank.GetByCategoryAsync("current_safety", incidentType, ct);
            var unaskedSafety = safetyQuestions
                .Where(q => q.IsActive && !IsDuplicateQuestion(q.QuestionBangla, askedQuestions))
                .Where(q => string.IsNullOrWhiteSpace(q.RequiredFactKey) || !IsFactAlreadyCollected(q.RequiredFactKey, collected))
                .ToList();

            if (unaskedSafety.Count > 0)
            {
                var q = unaskedSafety.OrderBy(x => x.Priority).First();
                return new FollowUpQuestionDecision(
                    IsComplete: false,
                    QuestionText: q.QuestionBangla,
                    QuestionId: q.QuestionId,
                    CategoryId: q.Category,
                    CategoryName: "জরুরি নিরাপত্তা",
                    PrioritizedMissingField: q.RequiredFactKey ?? "current_safety",
                    SelectionSource: $"AlarmingSafetyElevated:QuestionBank:{q.QuestionId}",
                    Reasoning: $"[Alarming Signal Elevated] High-priority safety question selected from Question Bank: {q.Category} ({q.QuestionId})",
                    MissingInformationFound: unaskedParentQuestions.Select(x => x.RequiredFactKey ?? x.Category).ToList()
                );
            }
        }

        // ---------------------------------------------------------------------
        // STEP 5: Select Next Eligible Question for Active Category
        // ---------------------------------------------------------------------
        if (unaskedParentQuestions.Count > 0)
        {
            var bestQuestion = unaskedParentQuestions
                .Select(q => new
                {
                    Item = q,
                    Score = CalculateContextScore(q, combinedText)
                })
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Item.Priority)
                .First()
                .Item;

            var categoryDef = _categoryService.GetCategory(bestQuestion.Category);

            _logger.LogInformation(
                "Step 5: Selected question '{QuestionId}' from Question Bank for parent category '{ParentCode}' (FactKey: {FactKey}, Priority: {Priority})",
                bestQuestion.QuestionId, parentCode, bestQuestion.RequiredFactKey, bestQuestion.Priority);

            return new FollowUpQuestionDecision(
                IsComplete: false,
                QuestionText: bestQuestion.QuestionBangla,
                QuestionId: bestQuestion.QuestionId,
                CategoryId: bestQuestion.Category,
                CategoryName: categoryDef?.DisplayName ?? bestQuestion.Category,
                PrioritizedMissingField: bestQuestion.RequiredFactKey ?? bestQuestion.Category,
                SelectionSource: $"QuestionBank:{bestQuestion.QuestionId}",
                Reasoning: $"Question selected from Question Bank according to priority rules: {bestQuestion.Category} ({bestQuestion.QuestionId})",
                MissingInformationFound: unaskedParentQuestions.Select(x => x.RequiredFactKey ?? x.Category).ToList()
            );
        }

        // ---------------------------------------------------------------------
        // STEP 6: Conclude Intake when all category questions are collected
        // ---------------------------------------------------------------------
        _logger.LogInformation("All candidate follow-up questions have been exhausted for parent category '{ParentCode}'. Concluding intake.", parentCode);
        return new FollowUpQuestionDecision(
            IsComplete: true,
            QuestionText: null,
            QuestionId: null,
            CategoryId: "completed",
            CategoryName: "Intake Complete",
            PrioritizedMissingField: null,
            SelectionSource: "ExhaustedIntakeComplete",
            Reasoning: "All available questions for the incident category have been addressed.",
            MissingInformationFound: []
        );
    }

    // -------------------------------------------------------------------------
    // Helper Methods
    // -------------------------------------------------------------------------

    private static string ResolveParentCategoryCode(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return "OTHER";
        var trimmed = rawCategory.Trim().ToUpperInvariant();
        if (trimmed is "DOWER_MAINTENANCE" or "DOWRY_DOMESTIC_VIOLENCE" or "LABOUR_WAGE" or "LAND_PROPERTY" or "WOMEN_CHILD_PROTECTION" or "OTHER")
            return trimmed;
        if (trimmed.Contains("DOWRY") || trimmed.Contains("যৌতুক") || trimmed.Contains("সহিংসতা") || trimmed.Contains("ABUSE") || trimmed.Contains("DOMESTIC"))
            return "DOWRY_DOMESTIC_VIOLENCE";
        if (trimmed.Contains("DOWER") || trimmed.Contains("MAINTENANCE") || trimmed.Contains("দেনমোহর") || trimmed.Contains("ভরণপোষণ") || trimmed.Contains("খোরপোষ"))
            return "DOWER_MAINTENANCE";
        if (trimmed.Contains("WAGE") || trimmed.Contains("LABOUR") || trimmed.Contains("LABOR") || trimmed.Contains("মজুরি") || trimmed.Contains("শ্রম") || trimmed.Contains("বেতন"))
            return "LABOUR_WAGE";
        if (trimmed.Contains("LAND") || trimmed.Contains("PROPERTY") || trimmed.Contains("জমি") || trimmed.Contains("সম্পত্তি"))
            return "LAND_PROPERTY";
        if (trimmed.Contains("CHILD") || trimmed.Contains("PROTECTION") || trimmed.Contains("সুরক্ষা") || trimmed.Contains("হেফাজত") || trimmed.Contains("নারী ও শিশু"))
            return "WOMEN_CHILD_PROTECTION";
        return "OTHER";
    }

    private static bool IsFactAlreadyCollected(string? field, Dictionary<string, string> collected)
    {
        if (string.IsNullOrWhiteSpace(field)) return false;

        var cleanKey = field.Trim();

        // Direct check
        if (collected.TryGetValue(cleanKey, out var val) && !string.IsNullOrWhiteSpace(val) && !IsGenericPlaceholderName(val) && val != "তথ্য পাওয়া যায়নি")
        {
            return true;
        }

        // Case-insensitive dictionary check
        foreach (var (k, v) in collected)
        {
            if (string.Equals(k, cleanKey, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(v) && !IsGenericPlaceholderName(v) && v != "তথ্য পাওয়া যায়নি")
            {
                return true;
            }
        }

        // Aliases
        var norm = cleanKey.ToLowerInvariant();
        if (norm is "respondent" or "perpetrator" or "employername" or "husbandname")
        {
            return (collected.TryGetValue("respondent", out val) && !string.IsNullOrWhiteSpace(val) && !IsGenericPlaceholderName(val)) ||
                   (collected.TryGetValue("employerName", out val) && !string.IsNullOrWhiteSpace(val) && !IsGenericPlaceholderName(val));
        }

        if (norm is "applicant" or "victim" or "workername" or "wife")
        {
            return collected.TryGetValue("applicant", out val) && !string.IsNullOrWhiteSpace(val) && !IsGenericPlaceholderName(val);
        }

        if (norm is "marriagedate" or "incidentdate" or "date" or "incident_date" or "marriage_date")
        {
            return (collected.TryGetValue("marriageDate", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("incidentDate", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("incident_date", out val) && !string.IsNullOrWhiteSpace(val));
        }

        if (norm is "doweramount" or "unpaidwageamount" or "disputedamount" or "disputed_amount")
        {
            return (collected.TryGetValue("dowerAmount", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("unpaidWageAmount", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("disputedAmount", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("disputed_amount", out val) && !string.IsNullOrWhiteSpace(val));
        }

        if (norm is "evidence" or "evidencelist" or "documents" or "nikahnama")
        {
            return (collected.TryGetValue("evidenceList", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("evidence", out val) && !string.IsNullOrWhiteSpace(val));
        }

        if (norm is "desiredrelief" or "desired_relief" or "relief")
        {
            return (collected.TryGetValue("desiredRelief", out val) && !string.IsNullOrWhiteSpace(val)) ||
                   (collected.TryGetValue("desired_relief", out val) && !string.IsNullOrWhiteSpace(val));
        }

        return false;
    }

    private static bool IsGenericPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        var placeholders = new[] { "ভুক্তভোগী", "ভুক্তভোগী নাগরিক", "নাগরিক", "বাদী", "আবেদনকারী", "অভিযোগকারী", "বিবাদী", "অভিযুক্ত", "প্রতিপক্ষ", "মালিক", "অজ্ঞাত", "অজ্ঞাতনামা", "তথ্য পাওয়া যায়নি" };
        return placeholders.Any(p => trimmed.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureMissingIfNotCollected(string field, Dictionary<string, string> collected, HashSet<string> missingSet)
    {
        if (!IsFactAlreadyCollected(field, collected))
        {
            missingSet.Add(field);
        }
    }

    private static string MapFactToCategory(string fact)
    {
        var normalized = fact.Trim().ToLowerInvariant();
        return FactToCategoryMap.TryGetValue(normalized, out var category) ? category : "incident_description";
    }

    private static bool HasDangerSignals(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] dangerKeywords =
        [
            "মারধর", "মেরে ফেল", "হত্যা", "রক্ত", "আঘাত", "হাসপাতাল", "ওসিসি", "ভয়", "ভয়",
            "হুমকি", "তাড়িয়ে", "বের করে", "আক্রমণ", "ঝুঁকি", "বিপদ", "মারবে", "গলা টিপে", "আত্মহত্যা"
        ];

        return dangerKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDuplicateQuestion(string candidate, IEnumerable<string> askedQuestions)
    {
        var normCandidate = NormalizeQuestionText(candidate);
        if (string.IsNullOrWhiteSpace(normCandidate)) return true;

        foreach (var asked in askedQuestions)
        {
            var normAsked = NormalizeQuestionText(asked);
            if (normAsked == normCandidate) return true;

            // Substring match for substantial question prompts (>15 characters)
            if (normCandidate.Length > 15 && normAsked.Length > 15)
            {
                if (normCandidate.Contains(normAsked) || normAsked.Contains(normCandidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizeQuestionText(string text) =>
        text.Trim()
            .Replace("?", "")
            .Replace("।", "")
            .Replace("!", "")
            .Replace(",", "")
            .Replace(";", "")
            .Replace(" ", "")
            .ToLowerInvariant();

    private static int CalculateContextScore(QuestionBankItem item, string combinedContext)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(combinedContext) && item.ContextKeywords.Count > 0)
        {
            foreach (var kw in item.ContextKeywords)
            {
                if (combinedContext.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    score += 15; // Context keyword match boost
                }
            }
        }

        // Lower priority number = better ranking
        score -= item.Priority;
        return score;
    }

    private static string BuildContextText(string? userTranscript, ConversationState state)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(userTranscript)) list.Add(userTranscript);

        if (state.InformationAlreadyCollected.TryGetValue("latest_user_update", out var update) && !string.IsNullOrWhiteSpace(update))
            list.Add(update);

        if (state.InformationAlreadyCollected.TryGetValue("key_facts", out var facts) && !string.IsNullOrWhiteSpace(facts))
            list.Add(facts);

        return string.Join(" ", list);
    }
}
