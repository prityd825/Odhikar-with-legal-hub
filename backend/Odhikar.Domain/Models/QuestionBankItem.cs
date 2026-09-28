using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Represents an intake follow-up question entry in the MongoDB Question Bank.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class QuestionBankItem
{
    [BsonId]
    [BsonIgnoreIfDefault]
    public object? Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Unique human-readable question identifier (e.g. "Q-DOWER-001", "Q-DOWRY-002", "Q-SAFETY-001").
    /// </summary>
    [BsonElement("questionId")]
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>
    /// Question category code (e.g. "current_safety", "threat_details", "injury_details",
    /// "perpetrator", "incident_description", "incident_time", "incident_location",
    /// "financial_details", "evidence_documents", "witness_details", "desired_relief").
    /// </summary>
    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The pre-approved Bengali question text presented to the client.
    /// </summary>
    [BsonElement("questionBangla")]
    public string QuestionBangla { get; set; } = string.Empty;

    /// <summary>
    /// Applicable high-level parent incident category codes (e.g. ["DOWER_MAINTENANCE", "DOWRY_DOMESTIC_VIOLENCE", "ALL"]).
    /// Maps directly to the codes in the MongoDB 'parent_incident_categories' collection.
    /// </summary>
    [BsonElement("parentCategoryCodes")]
    public List<string> ParentCategoryCodes { get; set; } = [];

    /// <summary>
    /// Applicable legal incident types e.g. ["DOWER_MAINTENANCE", "DOWRY_DOMESTIC_VIOLENCE", "ALL"].
    /// </summary>
    [BsonElement("relatedIncidentTypes")]
    public List<string> RelatedIncidentTypes { get; set; } = [];

    /// <summary>
    /// Priority weight within the category (lower number indicates higher precedence).
    /// </summary>
    [BsonElement("priority")]
    public int Priority { get; set; } = 10;

    /// <summary>
    /// Active status flag.
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional keywords that boost this question's relevance when detected in conversation context.
    /// </summary>
    [BsonElement("contextKeywords")]
    public List<string> ContextKeywords { get; set; } = [];

    /// <summary>
    /// Corresponding fact key in ConversationState.InformationAlreadyCollected.
    /// If this fact is already known, this question is filtered out.
    /// </summary>
    [BsonElement("requiredFactKey")]
    [BsonIgnoreIfNull]
    public string? RequiredFactKey { get; set; }

    /// <summary>
    /// Explanatory note or legal objective of this question.
    /// </summary>
    [BsonElement("notes")]
    [BsonIgnoreIfNull]
    public string? Notes { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
