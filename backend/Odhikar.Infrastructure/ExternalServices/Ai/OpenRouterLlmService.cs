using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.ExternalServices.Ai;

/// <summary>
/// OpenRouter-backed conversational intelligence and structured legal NLU service.
/// Analyzes user conversation transcripts to produce validated structured insights:
/// - Incident classification
/// - Extracted information
/// - Missing information
/// - Suggested next question category
/// - Conversation completion status
/// </summary>
public sealed class OpenRouterLlmService(
    HttpClient http,
    IOptions<AiSettings> settings,
    ILogger<OpenRouterLlmService> logger) : IOpenRouterLlmService
{
    private readonly HttpClient _http = http;
    private readonly AiSettings _settings = settings.Value;
    private readonly ILogger<OpenRouterLlmService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private const string SystemPrompt = """
        You are the Legal Intelligence & Analysis Engine of "Odhikar" (অধিকার), an empathetic legal aid digital assistant in Bangladesh.
        Analyze the client's input and conversation context to produce structured JSON strictly matching the specified schema.
        Return STRUCTURED DATA ONLY. Do not prepend or append markdown code blocks, conversational greetings, or explanations outside the JSON object.

        LEGAL CATEGORIES:
        - "DOWER_MAINTENANCE" (দেনমোহর ও খোরপোষ)
        - "DOWRY" (যৌতুক ও পারিবারিক নির্যাতন)
        - "UNPAID_WAGES" (বকেয়া মজুরি)
        - "LAND_DISPUTE" (জমি ও সম্পত্তি বিরোধ)
        - "OUT_OF_SCOPE" (Criminal matters, corporate, tax, immigration, or anything outside the 4 in-scope legal areas)
        - "UNKNOWN" (Insufficient facts to determine category yet)

        SEVERITY LEVELS:
        - "Critical" (Threat to life, severe physical violence, starvation, unlawful detention)
        - "High" (Eviction from marital home, homelessness risk, continuous harassment, total financial deprivation)
        - "Medium" (Unpaid wages dispute, land boundary dispute, refusal to pay dower without physical danger)
        - "Low" (General legal inquiry, procedural advice, preliminary rights consultation)

        QUESTION CATEGORIES:
        - "current_safety" (Current safety status, risk of violence, fear, or need for safe shelter)
        - "threat_details" (Explicit verbal threats, intimidation, mode or timing of threats)
        - "injury_details" (Physical injuries, bodily harm, assault, medical care taken)
        - "perpetrator" (Accused party, husband, employer, respondent name, identity, address)
        - "incident_description" (Core factual narrative of what occurred and how it began)
        - "incident_time" (Date, time, timeline of marriage or occurrence)
        - "incident_location" (District, police station/thana, or venue of incident)
        - "financial_details" (Dower amount, unpaid wages, claimed compensation amount)
        - "evidence_documents" (Kabinnama, contracts, photos, receipts, official documents)
        - "witness_details" (Eyewitnesses, bystanders, or local community witnesses)
        - "desired_relief" (Specific remedy, mediation, maintenance, or outcome sought)

        ANALYSIS CRITERIA:
        1. Classify the incident accurately with a confidence score (0.0 to 1.0) and identify ALL applicable incident types.
        2. Assess severity ("Low", "Medium", "High", "Critical") with specific reasoning.
        3. Extract all important facts mentioned in the conversation.
        4. Identify what NEW information was provided specifically in the user's latest answer.
        5. Identify missing critical facts required for legal complaint drafting.
        6. ANTI-DUPLICATE RULE: Inspect "QUESTIONS ALREADY ASKED" and "INFORMATION ALREADY COLLECTED". Never repeat a question or ask for facts already known. Select an unaddressed missing category.
        7. If information is missing, formulate exactly ONE concise, warm, natural follow-up question in colloquial Bangla.
        8. INFORMATION SUFFICIENCY & COMPLETION: Determine whether ENOUGH INFORMATION has been collected to formulate a formal legal notice or petition.
           - If essential facts (such as opposing party identity, timeline/amounts) are gathered, set "conversationComplete": true, "isInformationSufficient": true, and "suggestedNextQuestionCategory": "none".
           - If critical information is still missing, set "conversationComplete": false, populate "missingInformation", and set "suggestedNextQuestionCategory" to the next category code needed.

        OUTPUT FORMAT:
        Reply STRICTLY with a single valid JSON object matching this schema:
        {
          "incidentClassification": {
            "categoryCode": "DOWER_MAINTENANCE|DOWRY|UNPAID_WAGES|LAND_DISPUTE|OUT_OF_SCOPE|UNKNOWN",
            "categoryNameBangla": "বাংলা ক্যাটাগরি নাম",
            "confidence": 0.95,
            "explanation": "Brief reason for this classification",
            "relevantIncidentTypes": [ "DOWER_MAINTENANCE" ],
            "severity": "Low|Medium|High|Critical",
            "severityReason": "Brief explanation of severity assessment"
          },
          "relevantIncidentTypes": [ "DOWER_MAINTENANCE" ],
          "severity": "Low|Medium|High|Critical",
          "severityReason": "Brief explanation of severity assessment",
          "extractedInformation": {
            "applicant": "Name/identity of applicant or null",
            "respondent": "Name/identity of respondent or null",
            "incidentDate": "Date/period of marriage or incident or null",
            "disputedAmount": "Claimed amount in BDT or land size or null",
            "location": "Incident location/district or null",
            "importantFactsMentioned": [ "Fact 1", "Fact 2" ],
            "newInformationProvided": [ "New fact introduced in this specific answer" ],
            "keyFacts": [ "Fact 1", "Fact 2" ],
            "evidenceList": [ "Proof 1" ]
          },
          "importantFactsMentioned": [ "Fact 1", "Fact 2" ],
          "newInformationProvided": [ "New fact introduced in this specific answer" ],
          "missingInformation": {
            "missingFields": [ "field1", "field2" ],
            "isInformationSufficient": false,
            "missingDetailsDescription": "Brief description of missing elements"
          },
          "suggestedNextQuestionCategory": "current_safety|threat_details|injury_details|perpetrator|incident_description|incident_time|incident_location|financial_details|evidence_documents|witness_details|desired_relief|none",
          "conversationComplete": false,
          "suggestedNextQuestion": {
            "questionCategory": "current_safety|threat_details|injury_details|perpetrator|incident_description|incident_time|incident_location|financial_details|evidence_documents|witness_details|desired_relief|none",
            "questionBangla": "বাংলা ভাষায় একটিমাত্র প্রাসঙ্গিক প্রশ্ন",
            "reasoning": "Why this category is prioritized next"
          },
          "completionStatus": {
            "isComplete": false,
            "readinessPercentage": 40,
            "completionReason": "Explanation of readiness or completion state"
          }
        }
        """;

    public async Task<OpenRouterAnalysisResult> AnalyzeTurnAsync(OpenRouterAnalysisRequest request, CancellationToken ct = default)
    {
        var history = request.History?
            .Select(h => new ConversationMessage
            {
                Role = h.Role,
                Text = h.Text,
                InputType = h.InputType ?? h.MessageType,
                Timestamp = h.Timestamp
            })
            .ToList();

        return await AnalyzeConversationAsync(request.ConversationText, request.IncidentType, history, state: null, ct);
    }

    public async Task<OpenRouterAnalysisResult> AnalyzeConversationAsync(
        string conversationText,
        string? incidentType = null,
        IReadOnlyList<ConversationMessage>? history = null,
        ConversationState? state = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversationText))
        {
            return CreateFallbackResult(incidentType, "ইনপুট টেক্সট খালি রাখা যাবে না।");
        }

        if (string.IsNullOrWhiteSpace(_settings.OpenRouterApiKey))
        {
            _logger.LogWarning("OpenRouter API key is not configured (Ai:OpenRouterApiKey / Ai__OpenRouterApiKey). Providing local fallback analysis.");
            return CreateLocalFallback(conversationText, incidentType, history, state);
        }

        var userPromptBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(incidentType))
        {
            userPromptBuilder.AppendLine($"[Selected Preliminary Incident Category: {incidentType}]");
        }

        if (state != null)
        {
            if (state.InformationAlreadyCollected.Count > 0)
            {
                userPromptBuilder.AppendLine("INFORMATION ALREADY COLLECTED (DO NOT ASK FOR ANY OF THESE FACTS AGAIN):");
                foreach (var kvp in state.InformationAlreadyCollected)
                {
                    userPromptBuilder.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                userPromptBuilder.AppendLine();
            }

            if (state.QuestionsAlreadyAsked.Count > 0)
            {
                userPromptBuilder.AppendLine("QUESTIONS ALREADY ASKED (DO NOT REPEAT OR REPHRASE ANY OF THESE):");
                foreach (var q in state.QuestionsAlreadyAsked)
                {
                    userPromptBuilder.AppendLine($"- {q}");
                }
                userPromptBuilder.AppendLine();
            }
        }

        if (history != null && history.Count > 0)
        {
            userPromptBuilder.AppendLine("Conversation History (chronological order):");
            foreach (var msg in history.TakeLast(10))
            {
                var role = msg.Role.Equals("User", StringComparison.OrdinalIgnoreCase) ? "CLIENT" : "ASSISTANT";
                userPromptBuilder.AppendLine($"{role}: {msg.Text}");
            }
            userPromptBuilder.AppendLine();
        }

        userPromptBuilder.AppendLine("Current User Message:");
        userPromptBuilder.AppendLine(conversationText);

        var modelName = string.IsNullOrWhiteSpace(_settings.OpenRouterModel)
            ? "google/gemini-2.5-flash"
            : _settings.OpenRouterModel;

        var payload = new
        {
            model = modelName,
            max_tokens = 600,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPromptBuilder.ToString() }
            }
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.OpenRouterBaseUrl}/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouterApiKey);
            httpRequest.Headers.Add("HTTP-Referer", "https://odhikar.org");
            httpRequest.Headers.Add("X-Title", "Odhikar Legal Aid AI");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouter request failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
                return CreateLocalFallback(conversationText, incidentType, history);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";

            var cleanJson = JsonSanitizer.StripCodeFences(content);
            var parsed = JsonSerializer.Deserialize<OpenRouterAnalysisResult>(cleanJson, JsonOpts);

            if (parsed is null)
            {
                _logger.LogWarning("Failed to deserialize OpenRouter response JSON: {Content}", cleanJson);
                return CreateLocalFallback(conversationText, incidentType, history);
            }

            // Strongly-typed validation step before returning
            ValidateAndSanitize(parsed, conversationText, incidentType);
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter LLM analysis encountered an error. Falling back safely.");
            return CreateLocalFallback(conversationText, incidentType, history);
        }
    }

    /// <summary>
    /// Validates the LLM response against domain constraints, correcting or normalizing malformed fields.
    /// </summary>
    private static void ValidateAndSanitize(OpenRouterAnalysisResult result, string originalText, string? requestedIncident)
    {
        result.ValidationErrors ??= [];
        result.AnalyzedAt = DateTime.UtcNow;

        // 1. Validate Classification
        result.Classification ??= new IncidentClassification();
        if (string.IsNullOrWhiteSpace(result.Classification.CategoryCode) || !LegalCategories.IsValid(result.Classification.CategoryCode))
        {
            result.Classification.CategoryCode = MapToLegalCategory(requestedIncident ?? result.Classification.CategoryNameBangla);
            result.ValidationErrors.Add("CategoryCode was empty or invalid; normalized to recognized legal category.");
        }

        // Clamp confidence to [0.0, 1.0]
        result.Classification.Confidence = Math.Clamp(result.Classification.Confidence, 0.0, 1.0);
        if (result.Classification.Confidence == 0.0)
        {
            result.Classification.Confidence = 0.85;
        }

        if (string.IsNullOrWhiteSpace(result.Classification.CategoryNameBangla))
        {
            result.Classification.CategoryNameBangla = MapCategoryCodeToBangla(result.Classification.CategoryCode);
        }

        // 2. Validate Relevant Incident Types
        result.RelevantIncidentTypes ??= [];
        if (result.RelevantIncidentTypes.Count == 0 && result.Classification.RelevantIncidentTypes.Count > 0)
        {
            result.RelevantIncidentTypes = [.. result.Classification.RelevantIncidentTypes];
        }
        if (result.RelevantIncidentTypes.Count == 0)
        {
            result.RelevantIncidentTypes.Add(result.Classification.CategoryCode);
        }
        result.Classification.RelevantIncidentTypes = [.. result.RelevantIncidentTypes];

        // 3. Validate Severity
        var rawSeverity = result.Severity ?? result.Classification.Severity;
        result.Severity = rawSeverity?.Trim().ToLowerInvariant() switch
        {
            "critical" or "জরুরি" => "Critical",
            "high" or "উচ্চ" => "High",
            "low" or "স্বল্প" => "Low",
            _ => DetectSeverityFromText(originalText)
        };
        result.SeverityReason ??= result.Classification.SeverityReason ?? $"Assessed as {result.Severity} based on case facts.";
        result.Classification.Severity = result.Severity;
        result.Classification.SeverityReason = result.SeverityReason;

        // 4. Validate Extracted Information & Important Facts
        result.ExtractedInfo ??= new ExtractedInformation();
        result.ExtractedInfo.KeyFacts ??= [];
        result.ExtractedInfo.EvidenceList ??= [];

        result.ImportantFactsMentioned ??= [];
        if (result.ImportantFactsMentioned.Count == 0 && result.ExtractedInfo.ImportantFactsMentioned.Count > 0)
        {
            result.ImportantFactsMentioned = [.. result.ExtractedInfo.ImportantFactsMentioned];
        }
        if (result.ImportantFactsMentioned.Count == 0 && result.ExtractedInfo.KeyFacts.Count > 0)
        {
            result.ImportantFactsMentioned = [.. result.ExtractedInfo.KeyFacts];
        }
        if (result.ImportantFactsMentioned.Count == 0 && !string.IsNullOrWhiteSpace(originalText))
        {
            result.ImportantFactsMentioned.Add(originalText.Trim());
        }
        result.ExtractedInfo.ImportantFactsMentioned = [.. result.ImportantFactsMentioned];
        result.ExtractedInfo.KeyFacts = [.. result.ImportantFactsMentioned];

        // 5. Validate New Information Provided
        result.NewInformationProvided ??= [];
        if (result.NewInformationProvided.Count == 0 && result.ExtractedInfo.NewInformationProvided.Count > 0)
        {
            result.NewInformationProvided = [.. result.ExtractedInfo.NewInformationProvided];
        }
        if (result.NewInformationProvided.Count == 0 && !string.IsNullOrWhiteSpace(originalText))
        {
            result.NewInformationProvided.Add(originalText.Trim());
        }
        result.ExtractedInfo.NewInformationProvided = [.. result.NewInformationProvided];

        // 6. Validate Missing Information
        result.MissingInfo ??= new MissingInformation();
        result.MissingInfo.MissingFields ??= [];

        // 7. Validate Suggested Next Question
        result.NextQuestion ??= new SuggestedNextQuestion();
        if (string.IsNullOrWhiteSpace(result.NextQuestion.QuestionCategory))
        {
            result.NextQuestion.QuestionCategory = "Clarification";
        }

        if (string.IsNullOrWhiteSpace(result.NextQuestion.QuestionBangla) && !result.Completion.IsComplete)
        {
            result.NextQuestion.QuestionBangla = "আপনার সমস্যাটি সম্পর্কে আরও বিস্তারিত কিছু জানালে আইনি পরামর্শ দেওয়া সহজ হবে।";
            result.ValidationErrors.Add("QuestionBangla was empty while conversation is incomplete; default prompt supplied.");
        }

        // 8. Validate Completion Status
        result.Completion ??= new CompletionStatus();
        result.Completion.ReadinessPercentage = Math.Clamp(result.Completion.ReadinessPercentage, 0, 100);

        // Sanity check: If critical facts are missing, cannot be 100% complete
        if (result.Completion.IsComplete && result.MissingInfo.MissingFields.Count > 2)
        {
            result.Completion.IsComplete = false;
            result.ValidationErrors.Add("Model flagged isComplete=true but multiple essential fields remain missing.");
        }

        result.IsValid = result.ValidationErrors.Count == 0;
    }

    private static string DetectSeverityFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Low";
        var lower = text.ToLowerInvariant();

        if (lower.Contains("মারধর") || lower.Contains("হত্যা") || lower.Contains("রক্তাক্ত") ||
            lower.Contains("শারীরিক নির্যাতন") || lower.Contains("প্রাণনাশের হুমকি"))
        {
            return "Critical";
        }

        if (lower.Contains("বের করে দিয়ে") || lower.Contains("তাড়িয়ে দিয়ে") || lower.Contains("গৃহহীন") ||
            lower.Contains("না খেয়ে") || lower.Contains("হুমকি"))
        {
            return "High";
        }

        if (lower.Contains("টাকা") || lower.Contains("মজুরি") || lower.Contains("দেনমোহর") ||
            lower.Contains("জমি") || lower.Contains("দখল") || lower.Contains("খোরপোষ"))
        {
            return "Medium";
        }

        return "Low";
    }

    private static OpenRouterAnalysisResult CreateLocalFallback(
        string conversationText, string? incidentType, IReadOnlyList<ConversationMessage>? history, ConversationState? state = null)
    {
        var categoryCode = MapToLegalCategory(incidentType);
        var banglaName = MapCategoryCodeToBangla(categoryCode);
        var totalTurns = (history?.Count ?? 0) / 2 + 1;
        var isComplete = false;
        var severity = DetectSeverityFromText(conversationText);

        var asked = new HashSet<string>(state?.QuestionsAlreadyAsked ?? [], StringComparer.OrdinalIgnoreCase);

        var candidateQuestions = new List<(string Category, string Question)>
        {
            ("incident_time", categoryCode switch
            {
                LegalCategories.DowerMaintenance => "আপনাদের বিয়ে কত সালে হয়েছিল এবং কাবিনে দেনমোহরের পরিমাণ কত ধার্য ছিল?",
                LegalCategories.Dowry => "অভিযুক্ত ব্যক্তি কবে বা কত টাকা যৌতুক দাবি করেছে?",
                LegalCategories.UnpaidWages => "আপনার প্রতি মাসের মজুরি কত টাকা ছিল এবং মোট কত টাকা বকেয়া রয়েছে?",
                LegalCategories.LandDispute => "বিরোধপূর্ণ জমিটি কার দখলে আছে এবং এর কোনো খতিয়ান বা দলিল রয়েছে কি?",
                _ => "ঘটনাটির স্থান, তারিখ ও অপর পক্ষের নাম পরিচয় সম্পর্কে বিস্তারিত বলবেন কি?"
            }),
            ("perpetrator", "অপর পক্ষের পুরো নাম, পরিচয়, ঠিকানা বা কর্মস্থল কি জানা আছে?"),
            ("financial_details", "দাবিকৃত বা ক্ষতির আর্থিক পরিমাণ (যেমন দেনমোহর বা বকেয়া বেতন) আনুমানিক কত টাকা?"),
            ("evidence_documents", "এই বিষয়ে আপনার কাছে কি কোনো কাবিননামা, স্লিপ, চুক্তিপত্র বা প্রমাণপত্র রয়েছে?"),
            ("desired_relief", "আপনি এই বিষয়ে মূলত কী ধরনের আইনি প্রতিকার বা সমাধান চাচ্ছেন?")
        };

        var selected = candidateQuestions.FirstOrDefault(c => !asked.Contains(c.Question));
        var nextCategory = selected.Category ?? "incident_description";
        var nextQuestion = selected.Question ?? "আপনার সমস্যাটি সম্পর্কে আর কোনো প্রয়োজনীয় তথ্য কি জানাতে চান?";

        var result = new OpenRouterAnalysisResult
        {
            Classification = new IncidentClassification
            {
                CategoryCode = categoryCode,
                CategoryNameBangla = banglaName,
                Confidence = 0.80,
                Explanation = "Local fallback classification based on incident context and keywords.",
                RelevantIncidentTypes = [categoryCode],
                Severity = severity,
                SeverityReason = $"Assessed as {severity} from key incident indicators."
            },
            RelevantIncidentTypes = [categoryCode],
            Severity = severity,
            SeverityReason = $"Assessed as {severity} from key incident indicators.",
            ExtractedInfo = new ExtractedInformation
            {
                Applicant = null,
                KeyFacts = [conversationText],
                ImportantFactsMentioned = [conversationText],
                NewInformationProvided = [conversationText]
            },
            ImportantFactsMentioned = [conversationText],
            NewInformationProvided = [conversationText],
            MissingInfo = new MissingInformation
            {
                MissingFields = ["respondent", "incidentDate", "disputedAmount"],
                IsInformationSufficient = isComplete,
                MissingDetailsDescription = "Essential party names, dates, or documentation details are needed."
            },
            NextQuestion = new SuggestedNextQuestion
            {
                QuestionCategory = nextCategory,
                QuestionBangla = isComplete ? string.Empty : nextQuestion,
                Reasoning = "Gathering fundamental legal facts for complaint drafting."
            },
            Completion = new CompletionStatus
            {
                IsComplete = isComplete,
                ReadinessPercentage = Math.Min(totalTurns * 25, 100),
                CompletionReason = isComplete ? "Quota of intake turns reached." : "Additional key facts required."
            },
            IsValid = true
        };

        return result;
    }

    private static OpenRouterAnalysisResult CreateFallbackResult(string? incidentType, string message)
    {
        var category = MapToLegalCategory(incidentType);
        return new OpenRouterAnalysisResult
        {
            Classification = new IncidentClassification
            {
                CategoryCode = category,
                CategoryNameBangla = MapCategoryCodeToBangla(category),
                Confidence = 0.5,
                Explanation = message,
                RelevantIncidentTypes = [category],
                Severity = "Low",
                SeverityReason = message
            },
            RelevantIncidentTypes = [category],
            Severity = "Low",
            SeverityReason = message,
            NextQuestion = new SuggestedNextQuestion
            {
                QuestionCategory = "Clarification",
                QuestionBangla = "আপনার সমস্যাটি অনুগ্রহ করে আবার একটু স্পষ্ট করে বলবেন?",
                Reasoning = message
            },
            Completion = new CompletionStatus
            {
                IsComplete = false,
                ReadinessPercentage = 10,
                CompletionReason = message
            },
            IsValid = false,
            ValidationErrors = [message]
        };
    }

    private static string MapToLegalCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return LegalCategories.Unknown;
        var lower = category.ToLowerInvariant();

        if (lower.Contains("দেনমোহর") || lower.Contains("ভরণপোষণ") || lower.Contains("dower"))
            return LegalCategories.DowerMaintenance;
        if (lower.Contains("যৌতুক") || lower.Contains("dowry"))
            return LegalCategories.Dowry;
        if (lower.Contains("বেতন") || lower.Contains("মজুরি") || lower.Contains("wage"))
            return LegalCategories.UnpaidWages;
        if (lower.Contains("জমি") || lower.Contains("সম্পত্তি") || lower.Contains("land"))
            return LegalCategories.LandDispute;

        return LegalCategories.IsValid(category) ? category : LegalCategories.Unknown;
    }

    private static string MapCategoryCodeToBangla(string categoryCode) => categoryCode switch
    {
        LegalCategories.DowerMaintenance => "দেনমোহর / ভরণপোষণ",
        LegalCategories.Dowry => "যৌতুক ও পারিবারিক নির্যাতন",
        LegalCategories.UnpaidWages => "বকেয়া বেতন",
        LegalCategories.LandDispute => "জমি ও সম্পত্তি",
        LegalCategories.OutOfScope => "সেবাবহির্ভূত আইনি বিষয়",
        _ => "সাধারণ আইনি সমস্যা"
    };

    private const string ReportSystemPrompt = """
        You are the Senior Legal Report Generator for "Odhikar" (অধিকার), an empathetic legal aid platform in Bangladesh.
        Your task is to generate a comprehensive, strictly factual Final Legal Intake Report (আইনি অভিযোগ চূড়ান্ত প্রতিবেদন) for paralegals and legal aid lawyers based on a completed citizen conversation.

        STRICT ACCURACY AND ANTI-HALLUCINATION RULES:
        1. USE ONLY INFORMATION FROM THE CONVERSATION: Rely strictly on facts explicitly mentioned by the citizen in messages, recorded in conversation state, or captured in AI analyses.
        2. NEVER INVENT FACTS: Do NOT fabricate or assume dates, names, monetary amounts, injuries, or events that were not provided.
        3. MARK MISSING INFORMATION CLEARLY: Explicitly list any standard legal intake fields that were missing or not provided by the user.
        4. SUMMARIZE DETECTED INCIDENT TYPES: List all applicable legal dispute categories (e.g. DOWER_MAINTENANCE, DOWRY, UNPAID_WAGES, LAND_DISPUTE).
        5. SUMMARIZE IMPORTANT EVENTS: Detail key allegations, dispute points, and circumstances as factual bullet points.
        6. HIGHLIGHT SAFETY CONCERNS: Specifically assess threats, domestic abuse, physical harm, intimidation, or homelessness risks. If no safety threat was reported, state: "কোনো তাৎক্ষণিক শারীরিক নিরাপত্তা ঝুঁকি বা হুমকি রিপোর্ট করা হয়নি।"
        7. CHRONOLOGICAL SUMMARY: Provide a chronological timeline of events leading up to the dispute. If specific dates are absent, use relative temporal sequence.

        OUTPUT JSON SCHEMA (STRICTLY VALID JSON):
        {
          "title": "আইনি অভিযোগ চূড়ান্ত প্রতিবেদন",
          "primaryCategory": "DOWER_MAINTENANCE|DOWRY|UNPAID_WAGES|LAND_DISPUTE|OUT_OF_SCOPE|UNKNOWN",
          "detectedIncidentTypes": ["DOWER_MAINTENANCE"],
          "severity": "Low|Medium|High|Critical",
          "severityReason": "Reason for severity rating",
          "summary": "Full narrative summary of the dispute in formal Bangla",
          "importantEvents": [
            "Event 1 in Bangla",
            "Event 2 in Bangla"
          ],
          "chronologicalTimeline": [
            {
              "dateOrTime": "তারিখ বা সময়কাল",
              "description": "ঘটনার বিবরণ",
              "sourceTurn": 1
            }
          ],
          "safetyConcerns": {
            "hasSafetyConcerns": false,
            "isEmergencyEscalated": false,
            "safetySummary": "শারীরিক নিরাপত্তা বা হুমকির বিবরণ",
            "riskFactors": ["ঝুঁকির বিষয়সমূহ"]
          },
          "missingInformation": [
            "অনুপস্থিত তথ্য ১",
            "অনুপস্থিত তথ্য ২"
          ],
          "extractedFacts": {
            "applicant": "আবেদনকারী",
            "respondent": "প্রতিপক্ষ",
            "incidentDate": "তারিখ",
            "disputedAmount": "টাকার পরিমাণ",
            "location": "স্থান"
          },
          "legalAssessment": "প্রযোজ্য আইন ও আইনি পরামর্শ",
          "recommendedNextSteps": [
            "পদক্ষেপ ১",
            "পদক্ষেপ ২"
          ]
        }
        """;

    public async Task<FinalConversationReport> GenerateFinalReportAsync(
        ConversationModel conversation,
        ConversationState state,
        IReadOnlyList<AIAnalysis> analyses,
        CancellationToken ct = default)
    {
        var conversationId = conversation.Id;
        _logger.LogInformation("Generating Final Conversation Report for conversation {ConversationId} (Turns: {Turns}, Incident: '{Incident}')",
            conversationId, state.TurnCount, conversation.IncidentType);

        if (string.IsNullOrWhiteSpace(_settings.OpenRouterApiKey))
        {
            _logger.LogWarning("OpenRouter API key is not configured. Generating local fallback report for conversation {ConversationId}.", conversationId);
            return CreateLocalFallbackReport(conversation, state, analyses);
        }

        try
        {
            var userPromptBuilder = new StringBuilder();
            userPromptBuilder.AppendLine("=== ODHIKAR CONVERSATION INTAKE DOSSIER ===");
            userPromptBuilder.AppendLine($"Conversation ID: {conversation.Id}");
            userPromptBuilder.AppendLine($"Case Reference Number: {conversation.CaseId ?? "Pending"}");
            userPromptBuilder.AppendLine($"Selected Incident Type: {conversation.IncidentType}");
            userPromptBuilder.AppendLine($"Completion Reason: {state.CompletionReason ?? "Completed"}");
            userPromptBuilder.AppendLine($"Total Turns Completed: {state.TurnCount}");
            userPromptBuilder.AppendLine();

            userPromptBuilder.AppendLine("--- VERIFIED EXTRACTED FACTS (FROM CONVERSATION STATE) ---");
            if (state.InformationAlreadyCollected.Count > 0)
            {
                foreach (var kvp in state.InformationAlreadyCollected)
                {
                    userPromptBuilder.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                userPromptBuilder.AppendLine("None explicitly recorded in state dictionary.");
            }
            userPromptBuilder.AppendLine();

            userPromptBuilder.AppendLine("--- DETECTED INCIDENT TYPES ---");
            var incTypes = state.DetectedIncidentTypes.Count > 0
                ? string.Join(", ", state.DetectedIncidentTypes)
                : conversation.IncidentType;
            userPromptBuilder.AppendLine(incTypes);
            userPromptBuilder.AppendLine();

            userPromptBuilder.AppendLine("--- MISSING INFORMATION IDENTIFIED DURING INTAKE ---");
            if (state.MissingInformation.Count > 0)
            {
                foreach (var m in state.MissingInformation)
                {
                    userPromptBuilder.AppendLine($"- {m}");
                }
            }
            else
            {
                userPromptBuilder.AppendLine("No critical missing fields flagged.");
            }
            userPromptBuilder.AppendLine();

            userPromptBuilder.AppendLine("--- COMPLETE CONVERSATION TRANSCRIPT (CHRONOLOGICAL) ---");
            var sortedMessages = conversation.Messages
                .OrderBy(m => m.SequenceNumber)
                .ThenBy(m => m.Timestamp)
                .ToList();

            foreach (var msg in sortedMessages)
            {
                var role = msg.Role.Equals("User", StringComparison.OrdinalIgnoreCase) ? "CITIZEN / CLIENT" : "ODHIKAR ASSISTANT";
                userPromptBuilder.AppendLine($"[Seq {msg.SequenceNumber}] {role} ({msg.Timestamp:yyyy-MM-dd HH:mm:ss}):");
                userPromptBuilder.AppendLine(msg.Text);
                userPromptBuilder.AppendLine();
            }

            if (analyses.Count > 0)
            {
                userPromptBuilder.AppendLine("--- HISTORICAL AI ANALYSES & NLU MILESTONES ---");
                foreach (var a in analyses)
                {
                    userPromptBuilder.AppendLine($"* Analysis (Severity: {a.Severity}, Category: {a.Category}):");
                    if (a.ImportantFactsMentioned.Count > 0)
                    {
                        userPromptBuilder.AppendLine($"  Facts: {string.Join("; ", a.ImportantFactsMentioned)}");
                    }
                    if (a.NewInformationProvided.Count > 0)
                    {
                        userPromptBuilder.AppendLine($"  New Info: {string.Join("; ", a.NewInformationProvided)}");
                    }
                }
                userPromptBuilder.AppendLine();
            }

            var modelName = string.IsNullOrWhiteSpace(_settings.OpenRouterModel)
                ? "google/gemini-2.5-flash"
                : _settings.OpenRouterModel;

            var payload = new
            {
                model = modelName,
                max_tokens = 800,
                temperature = 0.1,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = ReportSystemPrompt },
                    new { role = "user", content = userPromptBuilder.ToString() }
                }
            };

            using var requestMsg = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenRouterApiKey);
            requestMsg.Headers.Add("HTTP-Referer", "https://odhikar.legal");
            requestMsg.Headers.Add("X-Title", "Odhikar Legal Aid");

            using var responseMsg = await _http.SendAsync(requestMsg, ct);
            var responseContent = await responseMsg.Content.ReadAsStringAsync(ct);

            if (!responseMsg.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouter report generation failed with status {StatusCode}: {Error}. Falling back to local report.",
                    responseMsg.StatusCode, responseContent);
                return CreateLocalFallbackReport(conversation, state, analyses);
            }

            var report = ParseOpenRouterReportJson(responseContent, conversation, state);
            _logger.LogInformation("Successfully generated Final Conversation Report for conversation {ConversationId} (Events: {Events}, Timeline: {Timeline})",
                conversation.Id, report.ImportantEvents?.Count ?? 0, report.ChronologicalTimeline?.Count ?? 0);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating Final Conversation Report with OpenRouter for {ConversationId}. Using local fallback.",
                conversation.Id);
            return CreateLocalFallbackReport(conversation, state, analyses);
        }
    }

    private static FinalConversationReport ParseOpenRouterReportJson(
        string rawJson,
        ConversationModel conversation,
        ConversationState state)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            string? content = null;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentProp))
                {
                    content = contentProp.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = rawJson;
            }

            var parsed = JsonSerializer.Deserialize<OpenRouterFinalReportJson>(content, JsonOpts);

            var report = new FinalConversationReport
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                CaseId = conversation.CaseId,
                CaseNumber = conversation.CaseId,
                Title = string.IsNullOrWhiteSpace(parsed?.Title) ? "আইনি অভিযোগ চূড়ান্ত প্রতিবেদন" : parsed.Title,
                PrimaryCategory = string.IsNullOrWhiteSpace(parsed?.PrimaryCategory) ? conversation.Category : parsed.PrimaryCategory,
                DetectedIncidentTypes = parsed?.DetectedIncidentTypes is { Count: > 0 }
                    ? parsed.DetectedIncidentTypes
                    : (state.DetectedIncidentTypes.Count > 0 ? state.DetectedIncidentTypes : [conversation.IncidentType]),
                Severity = string.IsNullOrWhiteSpace(parsed?.Severity) ? "Low" : parsed.Severity,
                SeverityReason = parsed?.SeverityReason,
                Summary = parsed?.Summary ?? string.Empty,
                ImportantEvents = parsed?.ImportantEvents ?? [],
                ChronologicalTimeline = parsed?.ChronologicalTimeline?
                    .Select(t => new ReportTimelineEntry
                    {
                        DateOrTime = t.DateOrTime,
                        Description = t.Description ?? string.Empty,
                        SourceTurn = t.SourceTurn
                    })
                    .ToList() ?? [],
                SafetyConcerns = new ReportSafetySummary
                {
                    HasSafetyConcerns = parsed?.SafetyConcerns?.HasSafetyConcerns ?? conversation.IsSafetyEscalated,
                    IsEmergencyEscalated = parsed?.SafetyConcerns?.IsEmergencyEscalated ?? conversation.IsSafetyEscalated,
                    SafetySummary = parsed?.SafetyConcerns?.SafetySummary ?? (conversation.IsSafetyEscalated ? conversation.SafetyReason ?? "নিরাপত্তা সতর্কতা সক্রিয়" : "কোনো তাৎক্ষণিক শারীরিক নিরাপত্তা ঝুঁকি বা হুমকি রিপোর্ট করা হয়নি।"),
                    RiskFactors = parsed?.SafetyConcerns?.RiskFactors ?? []
                },
                MissingInformation = parsed?.MissingInformation ?? state.MissingInformation,
                ExtractedFacts = parsed?.ExtractedFacts != null && parsed.ExtractedFacts.Count > 0
                    ? parsed.ExtractedFacts
                    : state.InformationAlreadyCollected,
                LegalAssessment = parsed?.LegalAssessment,
                RecommendedNextSteps = parsed?.RecommendedNextSteps ?? [],
                TotalTurns = state.TurnCount,
                CompletionReason = state.CompletionReason,
                GeneratedAt = DateTime.UtcNow,
                RawReportJson = content
            };

            return report;
        }
        catch
        {
            return CreateLocalFallbackReport(conversation, state, []);
        }
    }

    private static FinalConversationReport CreateLocalFallbackReport(
        ConversationModel conversation,
        ConversationState state,
        IReadOnlyList<AIAnalysis> analyses)
    {
        var collected = state.InformationAlreadyCollected;
        var missing = state.MissingInformation;

        var events = new List<string>();
        if (collected.TryGetValue("key_facts", out var kf) && !string.IsNullOrWhiteSpace(kf))
        {
            events.Add(kf);
        }
        if (collected.TryGetValue("latest_user_update", out var lu) && !string.IsNullOrWhiteSpace(lu))
        {
            events.Add(lu);
        }
        if (events.Count == 0)
        {
            var userMsgs = conversation.Messages
                .Where(m => m.Role.Equals("User", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Text)
                .ToList();
            events.AddRange(userMsgs);
        }

        var timeline = new List<ReportTimelineEntry>();
        if (collected.TryGetValue("incident_date", out var idate) && !string.IsNullOrWhiteSpace(idate))
        {
            timeline.Add(new ReportTimelineEntry
            {
                DateOrTime = idate,
                Description = "ঘটনা বা বিরোধের সূত্রপাত",
                SourceTurn = 1
            });
        }
        timeline.Add(new ReportTimelineEntry
        {
            DateOrTime = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Description = "অধিকার প্ল্যাটফর্মে প্রাথমিক আইনি পরামর্শ ও তথ্য গ্রহণ সম্পন্ন",
            SourceTurn = state.TurnCount
        });

        var summary = $"আবেদনকারীর বিবরণ অনুযায়ী ঘটনাটি মূলত {conversation.IncidentType} সংক্রান্ত। " +
                      (collected.TryGetValue("respondent", out var resp) ? $"বিবাদী: {resp}। " : "") +
                      (collected.TryGetValue("disputedAmount", out var amt) ? $"দাবিকৃত পরিমাণ: {amt}। " : "") +
                      "নাগরিকের সাথে তথ্য গ্রহণ প্রক্রিয়া সমাপ্ত হয়েছে এবং প্রাথমিক খসড়া সংরক্ষণ করা হয়েছে।";

        return new FinalConversationReport
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            CaseId = conversation.CaseId,
            CaseNumber = conversation.CaseId,
            Title = "আইনি অভিযোগ চূড়ান্ত প্রতিবেদন",
            PrimaryCategory = conversation.Category,
            DetectedIncidentTypes = state.DetectedIncidentTypes.Count > 0 ? state.DetectedIncidentTypes : [conversation.IncidentType],
            Severity = conversation.Analysis.Severity ?? "Low",
            SeverityReason = conversation.Analysis.SeverityReason,
            Summary = summary,
            ImportantEvents = events,
            ChronologicalTimeline = timeline,
            SafetyConcerns = new ReportSafetySummary
            {
                HasSafetyConcerns = conversation.IsSafetyEscalated,
                IsEmergencyEscalated = conversation.IsSafetyEscalated,
                SafetySummary = conversation.IsSafetyEscalated ? (conversation.SafetyReason ?? "নিরাপত্তা সতর্কতা সক্রিয়") : "কোনো তাৎক্ষণিক শারীরিক নিরাপত্তা ঝুঁকি বা সহিংসতা রিপোর্ট করা হয়নি।",
                RiskFactors = conversation.IsSafetyEscalated ? [conversation.SafetyReason ?? "Danger signal"] : []
            },
            MissingInformation = missing,
            ExtractedFacts = new Dictionary<string, string>(collected, StringComparer.OrdinalIgnoreCase),
            LegalAssessment = "আইনি পরামর্শক দল সংগৃহীত তথ্যের ভিত্তিতে আনুষ্ঠানিক লিগ্যাল নোটিশ বা পারিবারিক আদালতে মামলা দায়েরের প্রস্তুতি গ্রহণ করতে পারেন।",
            RecommendedNextSteps =
            [
                "প্যারালিগ্যাল টিম দ্বারা প্রাপ্ত তথ্যের সত্যতা যাচাই",
                "প্রয়োজনীয় কাবিননামা বা কাগজপত্রের মূল কপি সংগ্রহ",
                "আইনি সহায়তা প্রদানকারী সংস্থার মাধ্যমে পরবর্তী পদক্ষেপ গ্রহণ"
            ],
            TotalTurns = state.TurnCount,
            CompletionReason = state.CompletionReason,
            GeneratedAt = DateTime.UtcNow,
            RawReportJson = null
        };
    }
}
