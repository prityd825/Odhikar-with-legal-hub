using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Aggregate root representing a persistent multi-turn conversational session
/// between a citizen and the Gemini AI assistant. Persisted to the 'conversations' collection.
/// </summary>
[BsonIgnoreExtraElements]
public class Conversation
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("incidentType")]
    public string IncidentType { get; set; } = "সাধারণ আইনি সমস্যা";

    [BsonElement("incidentCategoryCode")]
    [BsonIgnoreIfNull]
    public string? IncidentCategoryCode { get; set; }

    [BsonElement("caseId")]
    [BsonIgnoreIfNull]
    public string? CaseId { get; set; }

    [BsonElement("state")]
    public ConversationState State { get; set; } = new();

    [BsonElement("analysis")]
    public AIAnalysis Analysis { get; set; } = new();

    [BsonElement("messages")]
    public List<ConversationMessage> Messages { get; set; } = new();

    [BsonElement("finalReport")]
    [BsonIgnoreIfNull]
    public FinalConversationReport? FinalReport { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // -------------------------------------------------------------------------
    // Synchronized properties for full backward and forward compatibility
    // -------------------------------------------------------------------------

    [BsonElement("status")]
    public string Status
    {
        get => State?.Status ?? "Active";
        set { State ??= new(); State.Status = value; }
    }

    [BsonElement("isComplete")]
    public bool IsComplete
    {
        get => State?.IsComplete ?? false;
        set { State ??= new(); State.IsComplete = value; }
    }

    [BsonElement("isSafetyEscalated")]
    public bool IsSafetyEscalated
    {
        get => Analysis?.IsSafetyEscalated ?? false;
        set { Analysis ??= new(); Analysis.IsSafetyEscalated = value; }
    }

    [BsonElement("safetyReason")]
    [BsonIgnoreIfNull]
    public string? SafetyReason
    {
        get => Analysis?.SafetyReason;
        set { Analysis ??= new(); Analysis.SafetyReason = value; }
    }

    [BsonElement("category")]
    public string Category
    {
        get => Analysis?.Category ?? "OTHER";
        set { Analysis ??= new(); Analysis.Category = value; }
    }

    [BsonElement("extractedData")]
    [BsonIgnoreIfNull]
    public ExtractedLegalData? ExtractedData
    {
        get => Analysis?.ExtractedData;
        set { Analysis ??= new(); Analysis.ExtractedData = value; }
    }
}

/// <summary>
/// Existing ConversationModel subclass for backwards-compatibility.
/// Reuses the existing 'conversations' collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ConversationModel : Conversation
{
}

/// <summary>
/// A single turn message within an AI conversation.
/// Supports ConversationId, Role (User/System/Assistant), Text, InputType, and Timestamp.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ConversationMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    /// <summary>Role of the message sender: "User", "System", or "Assistant".</summary>
    [BsonElement("role")]
    public string Role { get; set; } = "User";

    /// <summary>Type of input: "Text" or "Voice".</summary>
    [BsonElement("inputType")]
    public string InputType { get; set; } = "Text";

    /// <summary>Backwards-compatible alias for InputType matching previous schema.</summary>
    [BsonElement("messageType")]
    public string MessageType
    {
        get => InputType;
        set => InputType = value;
    }

    /// <summary>The spoken transcript or typed text content.</summary>
    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Optional audio reference/URI if audio was recorded.</summary>
    [BsonElement("audioUrl")]
    [BsonIgnoreIfNull]
    public string? AudioUrl { get; set; }

    /// <summary>Chronological sequence number of the message within the conversation (1, 2, 3, ...).</summary>
    [BsonElement("sequenceNumber")]
    public int SequenceNumber { get; set; } = 1;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
