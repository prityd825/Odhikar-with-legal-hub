using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Persisted case summary record stored in the <c>generated_case_summaries</c> collection.
/// Generated from conversation answers and case summary templates.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class GeneratedCaseSummaryModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    [BsonElement("incidentCategoryCode")]
    public string IncidentCategoryCode { get; set; } = string.Empty;

    [BsonElement("incidentTypeCode")]
    [BsonIgnoreIfNull]
    public string? IncidentTypeCode { get; set; }

    [BsonElement("templateCode")]
    public string TemplateCode { get; set; } = string.Empty;

    [BsonElement("templateVersion")]
    public int TemplateVersion { get; set; } = 1;

    [BsonElement("structuredContent")]
    public List<GeneratedCaseSummaryField> StructuredContent { get; set; } = [];

    /// <summary>
    /// Professional Bangla narrative case summary synthesized from structured content.
    /// </summary>
    [BsonElement("generatedText")]
    public string GeneratedText { get; set; } = string.Empty;

    /// <summary>
    /// Workflow status: "DRAFT", "REVIEWED", "APPROVED", "ARCHIVED".
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "DRAFT";

    [BsonElement("completionRate")]
    public double CompletionRate { get; set; }

    [BsonElement("totalRequiredFields")]
    public int TotalRequiredFields { get; set; }

    [BsonElement("completedRequiredFields")]
    public int CompletedRequiredFields { get; set; }

    [BsonElement("missingRequiredFields")]
    public List<string> MissingRequiredFields { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual field within the structured content of a generated case summary.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class GeneratedCaseSummaryField
{
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("labelBn")]
    public string LabelBn { get; set; } = string.Empty;

    [BsonElement("labelEn")]
    [BsonIgnoreIfNull]
    public string? LabelEn { get; set; }

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("isRequired")]
    public bool IsRequired { get; set; }

    [BsonElement("fieldType")]
    public string FieldType { get; set; } = "Text";

    /// <summary>
    /// Value provided by the user, inferred from facts, or "তথ্য পাওয়া যায়নি" if unavailable.
    /// </summary>
    [BsonElement("value")]
    public string Value { get; set; } = "তথ্য পাওয়া যায়নি";

    /// <summary>
    /// Status: "Stated", "Inferred", or "Missing".
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "Missing";

    [BsonElement("isInferred")]
    public bool IsInferred { get; set; }

    [BsonElement("inferenceNote")]
    [BsonIgnoreIfNull]
    public string? InferenceNote { get; set; }

    [BsonElement("confidence")]
    [BsonIgnoreIfNull]
    public double? Confidence { get; set; }

    [BsonElement("sourceKey")]
    [BsonIgnoreIfNull]
    public string? SourceKey { get; set; }
}
