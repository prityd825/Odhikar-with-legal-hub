using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Encapsulates legal NLU analysis, classification confidence, safety screening, and extracted facts.
/// Can be embedded in a Conversation document or stored in the ai_analyses collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AIAnalysis
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    /// <summary>Classified legal category e.g. "DOWER_MAINTENANCE", "দেনমোহর / ভরণপোষণ".</summary>
    [BsonElement("category")]
    public string Category { get; set; } = "সাধারণ আইনি সমস্যা";

    /// <summary>Confidence score between 0.0 and 1.0.</summary>
    [BsonElement("categoryConfidence")]
    public double CategoryConfidence { get; set; } = 0.0;

    /// <summary>Applicable legal incident types identified by OpenRouter.</summary>
    [BsonElement("relevantIncidentTypes")]
    public List<string> RelevantIncidentTypes { get; set; } = [];

    /// <summary>Assessed case severity: Low, Medium, High, Critical.</summary>
    [BsonElement("severity")]
    [BsonIgnoreIfNull]
    public string? Severity { get; set; }

    /// <summary>Rationale for severity assessment.</summary>
    [BsonElement("severityReason")]
    [BsonIgnoreIfNull]
    public string? SeverityReason { get; set; }

    /// <summary>Important legal and contextual facts mentioned.</summary>
    [BsonElement("importantFactsMentioned")]
    public List<string> ImportantFactsMentioned { get; set; } = [];

    /// <summary>Specific new information provided in the latest user answer.</summary>
    [BsonElement("newInformationProvided")]
    public List<string> NewInformationProvided { get; set; } = [];

    /// <summary>List of missing critical fields required for case resolution.</summary>
    [BsonElement("missingFields")]
    public List<string> MissingFields { get; set; } = [];

    /// <summary>Detailed description of missing information.</summary>
    [BsonElement("missingDetailsDescription")]
    [BsonIgnoreIfNull]
    public string? MissingDetailsDescription { get; set; }

    /// <summary>True if enough facts are gathered according to AI analysis.</summary>
    [BsonElement("isInformationSufficient")]
    public bool IsInformationSufficient { get; set; }

    /// <summary>Suggested category for the next question (e.g. "financial_details", "perpetrator").</summary>
    [BsonElement("suggestedNextQuestionCategory")]
    [BsonIgnoreIfNull]
    public string? SuggestedNextQuestionCategory { get; set; }

    /// <summary>Whether the AI analysis concluded that the intake conversation is complete.</summary>
    [BsonElement("conversationComplete")]
    public bool ConversationComplete { get; set; }

    /// <summary>Whether backend validated the completion status against ConversationState.</summary>
    [BsonElement("isBackendValidated")]
    public bool IsBackendValidated { get; set; }

    /// <summary>Backend validation rationale or list of missing mandatory fields.</summary>
    [BsonElement("backendValidationReason")]
    [BsonIgnoreIfNull]
    public string? BackendValidationReason { get; set; }

    /// <summary>Indicates whether physical danger, violence, or emergency threat was detected.</summary>
    [BsonElement("isSafetyEscalated")]
    public bool IsSafetyEscalated { get; set; }

    /// <summary>Reason for safety escalation, if any.</summary>
    [BsonElement("safetyReason")]
    [BsonIgnoreIfNull]
    public string? SafetyReason { get; set; }

    /// <summary>Structured legal facts extracted across the multi-turn conversation.</summary>
    [BsonElement("extractedData")]
    [BsonIgnoreIfNull]
    public ExtractedLegalData? ExtractedData { get; set; }

    /// <summary>Complete strongly typed raw analysis from OpenRouter.</summary>
    [BsonElement("rawAnalysis")]
    [BsonIgnoreIfNull]
    public OpenRouterAnalysisResult? RawAnalysis { get; set; }

    /// <summary>Timestamp when the latest AI reasoning and analysis occurred.</summary>
    [BsonElement("analyzedAt")]
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
