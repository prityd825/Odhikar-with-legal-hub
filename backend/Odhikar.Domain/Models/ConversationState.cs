using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Tracks the dynamic execution state, step progress, and workflow status of an AI conversation.
/// Can be embedded in a Conversation document or stored in the conversation_states collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ConversationState
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    /// <summary>Workflow status e.g. "Active", "Completed", "SafetyEscalated".</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Current stage of the conversational intake flow:
    /// e.g. "Classification", "FactGathering", "EvidenceCollection", "Review", "Completed", "SafetyEscalated".
    /// </summary>
    [BsonElement("currentStage")]
    public string CurrentStage { get; set; } = "Classification";

    /// <summary>Current intake step number (1-based, e.g. ১, ২, ৩, ৪).</summary>
    [BsonElement("currentStep")]
    public int CurrentStep { get; set; } = 1;

    /// <summary>Total turns completed by user so far.</summary>
    [BsonElement("turnCount")]
    public int TurnCount { get; set; } = 0;

    /// <summary>All incident legal types detected across dialogue turns.</summary>
    [BsonElement("detectedIncidentTypes")]
    public List<string> DetectedIncidentTypes { get; set; } = [];

    /// <summary>Key-value store of all verified facts already collected (e.g. applicant, respondent, incidentDate, amount, keyFacts, evidence).</summary>
    [BsonElement("informationAlreadyCollected")]
    public Dictionary<string, string> InformationAlreadyCollected { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>List of critical legal facts or fields still missing.</summary>
    [BsonElement("missingInformation")]
    public List<string> MissingInformation { get; set; } = [];

    /// <summary>Detailed description of missing facts or gaps.</summary>
    [BsonElement("missingDetailsDescription")]
    [BsonIgnoreIfNull]
    public string? MissingDetailsDescription { get; set; }

    /// <summary>All system questions previously asked to the user in chronological order.</summary>
    [BsonElement("questionsAlreadyAsked")]
    public List<string> QuestionsAlreadyAsked { get; set; } = [];

    /// <summary>Categories of questions already asked (e.g. Identity, Timeline, Financial, Evidence).</summary>
    [BsonElement("askedQuestionCategories")]
    public List<string> AskedQuestionCategories { get; set; } = [];

    /// <summary>Indicates whether all intake questions have been completed.</summary>
    [BsonElement("isComplete")]
    public bool IsComplete { get; set; }

    /// <summary>Estimated readiness percentage of the intake complaint (0 to 100).</summary>
    [BsonElement("readinessPercentage")]
    public int ReadinessPercentage { get; set; } = 0;

    /// <summary>Reason explaining completion or pending status.</summary>
    [BsonElement("completionReason")]
    [BsonIgnoreIfNull]
    public string? CompletionReason { get; set; }

    /// <summary>True when the conversation is awaiting user response.</summary>
    [BsonElement("isAwaitingInput")]
    public bool IsAwaitingInput { get; set; } = true;

    /// <summary>The most recent question or prompt presented to the user.</summary>
    [BsonElement("nextPrompt")]
    [BsonIgnoreIfNull]
    public string? NextPrompt { get; set; }

    /// <summary>Timestamp of the last turn activity.</summary>
    [BsonElement("lastActiveAt")]
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
}
