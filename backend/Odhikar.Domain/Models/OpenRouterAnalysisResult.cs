using System.Text.Json.Serialization;

namespace Odhikar.Domain.Models;

/// <summary>
/// Strongly typed result of the OpenRouter LLM conversation analysis.
/// </summary>
public sealed class OpenRouterAnalysisResult
{
    [JsonPropertyName("incidentClassification")]
    public IncidentClassification Classification { get; set; } = new();

    /// <summary>Applicable legal incident types (e.g. DOWER_MAINTENANCE, DOWRY, UNPAID_WAGES, LAND_DISPUTE).</summary>
    [JsonPropertyName("relevantIncidentTypes")]
    public List<string> RelevantIncidentTypes { get; set; } = [];

    /// <summary>Assessed severity if applicable: Low, Medium, High, Critical.</summary>
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    /// <summary>Rationale for severity assessment.</summary>
    [JsonPropertyName("severityReason")]
    public string? SeverityReason { get; set; }

    [JsonPropertyName("extractedInformation")]
    public ExtractedInformation ExtractedInfo { get; set; } = new();

    /// <summary>Important facts mentioned throughout the dialogue.</summary>
    [JsonPropertyName("importantFactsMentioned")]
    public List<string> ImportantFactsMentioned { get; set; } = [];

    /// <summary>Specific new information provided in the latest user answer.</summary>
    [JsonPropertyName("newInformationProvided")]
    public List<string> NewInformationProvided { get; set; } = [];

    [JsonPropertyName("missingInformation")]
    public MissingInformation MissingInfo { get; set; } = new();

    /// <summary>Direct boolean indicating whether enough information has been collected according to AI analysis.</summary>
    [JsonPropertyName("conversationComplete")]
    public bool ConversationComplete
    {
        get => _conversationComplete || Completion.IsComplete;
        set
        {
            _conversationComplete = value;
            Completion.IsComplete = value;
        }
    }
    private bool _conversationComplete;

    /// <summary>The suggested next question category code (e.g. current_safety, perpetrator, financial_details).</summary>
    [JsonPropertyName("suggestedNextQuestionCategory")]
    public string? SuggestedNextQuestionCategory
    {
        get => !string.IsNullOrWhiteSpace(_suggestedCategory) ? _suggestedCategory : NextQuestion.QuestionCategory;
        set
        {
            _suggestedCategory = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                NextQuestion.QuestionCategory = value;
            }
        }
    }
    private string? _suggestedCategory;

    [JsonPropertyName("suggestedNextQuestion")]
    public SuggestedNextQuestion NextQuestion { get; set; } = new();

    [JsonPropertyName("completionStatus")]
    public CompletionStatus Completion { get; set; } = new();

    [JsonPropertyName("analyzedAt")]
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool IsValid { get; set; } = true;

    [JsonIgnore]
    public List<string> ValidationErrors { get; set; } = [];
}

/// <summary>
/// Classified incident legal domain, confidence, and severity.
/// </summary>
public sealed class IncidentClassification
{
    [JsonPropertyName("categoryCode")]
    public string CategoryCode { get; set; } = LegalCategories.Unknown;

    [JsonPropertyName("categoryNameBangla")]
    public string CategoryNameBangla { get; set; } = "সাধারণ আইনি সমস্যা";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }

    [JsonPropertyName("relevantIncidentTypes")]
    public List<string> RelevantIncidentTypes { get; set; } = [];

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("severityReason")]
    public string? SeverityReason { get; set; }
}

/// <summary>
/// Facts and legal entities extracted from user turns.
/// </summary>
public sealed class ExtractedInformation
{
    [JsonPropertyName("applicant")]
    public string? Applicant { get; set; }

    [JsonPropertyName("respondent")]
    public string? Respondent { get; set; }

    [JsonPropertyName("incidentDate")]
    public string? IncidentDate { get; set; }

    [JsonPropertyName("disputedAmount")]
    public string? DisputedAmount { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("keyFacts")]
    public List<string> KeyFacts { get; set; } = [];

    [JsonPropertyName("importantFactsMentioned")]
    public List<string> ImportantFactsMentioned { get; set; } = [];

    [JsonPropertyName("newInformationProvided")]
    public List<string> NewInformationProvided { get; set; } = [];

    [JsonPropertyName("evidenceList")]
    public List<string> EvidenceList { get; set; } = [];
}

/// <summary>
/// Gaps and missing details required for legal filing.
/// </summary>
public sealed class MissingInformation
{
    [JsonPropertyName("missingFields")]
    public List<string> MissingFields { get; set; } = [];

    [JsonPropertyName("isInformationSufficient")]
    public bool IsInformationSufficient { get; set; }

    [JsonPropertyName("missingDetailsDescription")]
    public string? MissingDetailsDescription { get; set; }
}

/// <summary>
/// Next recommended question and categorization.
/// </summary>
public sealed class SuggestedNextQuestion
{
    [JsonPropertyName("questionCategory")]
    public string QuestionCategory { get; set; } = "Clarification";

    [JsonPropertyName("questionBangla")]
    public string QuestionBangla { get; set; } = string.Empty;

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}

/// <summary>
/// Progress and completion assessment of the intake dialogue.
/// </summary>
public sealed class CompletionStatus
{
    [JsonPropertyName("isComplete")]
    public bool IsComplete { get; set; }

    [JsonPropertyName("readinessPercentage")]
    public int ReadinessPercentage { get; set; }

    [JsonPropertyName("completionReason")]
    public string? CompletionReason { get; set; }
}
