using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Normalized answer record stored in the <c>conversation_answers</c> collection.
/// Preserves conversation/session tracking, stable question code, question text,
/// selected choice option, answer text, timestamp, input source, and confidence score.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ConversationAnswerModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Optional reference to an ongoing AI Conversation.</summary>
    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    [BsonIgnoreIfNull]
    public Guid? ConversationId { get; set; }

    /// <summary>Optional reference to a follow-up questionnaire session.</summary>
    [BsonRepresentation(BsonType.String)]
    [BsonElement("sessionId")]
    [BsonIgnoreIfNull]
    public Guid? SessionId { get; set; }

    /// <summary>Optional reference to a promoted legal case.</summary>
    [BsonRepresentation(BsonType.String)]
    [BsonElement("caseId")]
    [BsonIgnoreIfNull]
    public Guid? CaseId { get; set; }

    /// <summary>
    /// Stable canonical question code (e.g. INCIDENT_VICTIM, INCIDENT_ACCUSED, DOWRY_DEMAND_AMOUNT, INCIDENT_TIME).
    /// Used directly as a lookup key for summary templates and legal complaint generation.
    /// </summary>
    [BsonElement("questionCode")]
    public string QuestionCode { get; set; } = string.Empty;

    /// <summary>Original Bangla question prompt presented to the user.</summary>
    [BsonElement("questionText")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Canonical answer text (typed text or selected choice label).</summary>
    [BsonElement("answer")]
    public string Answer { get; set; } = string.Empty;

    /// <summary>Selected integer option ID if answer was single-choice or multi-choice.</summary>
    [BsonElement("selectedOptionId")]
    [BsonIgnoreIfNull]
    public int? SelectedOptionId { get; set; }

    /// <summary>List of selected option IDs if multiple choices were picked.</summary>
    [BsonElement("selectedOptionIds")]
    public List<int> SelectedOptionIds { get; set; } = [];

    /// <summary>Text values of selected options.</summary>
    [BsonElement("selectedOptionTexts")]
    public List<string> SelectedOptionTexts { get; set; } = [];

    /// <summary>Timestamp when the user provided or confirmed the answer.</summary>
    [BsonElement("answeredAt")]
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Origin of the answer: "FollowupQuestionnaire", "AiIntakeVoice", "AiIntakeText", "ParalegalEntry".</summary>
    [BsonElement("source")]
    public string Source { get; set; } = "FollowupQuestionnaire";

    /// <summary>Confidence score between 0.0 and 1.0 (1.0 for explicit user choices).</summary>
    [BsonElement("confidence")]
    [BsonIgnoreIfNull]
    public double? Confidence { get; set; } = 1.0;

    /// <summary>Optional contextual metadata.</summary>
    [BsonElement("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = [];
}
