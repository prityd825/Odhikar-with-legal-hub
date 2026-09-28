using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>One exchange in the intake conversation.</summary>
[BsonIgnoreExtraElements]
public sealed class DialogueTurn
{
    [BsonElement("turnIndex")]
    public int TurnIndex { get; set; }

    /// <summary>URL/reference to the stored client audio for this turn (null for text turns).</summary>
    [BsonElement("audioUrl")]
    [BsonIgnoreIfNull]
    public string? AudioUrl { get; set; }

    /// <summary>The transcribed (or typed) client input for this turn.</summary>
    [BsonElement("transcript")]
    public string Transcript { get; set; } = string.Empty;

    /// <summary>The system's follow-up question or narrative response after processing the input.</summary>
    [BsonElement("systemPrompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>True if this specific turn tripped the safety guardrail.</summary>
    [BsonElement("safetyTriggered")]
    public bool SafetyTriggered { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
