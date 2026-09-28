using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Configuration-driven template defining the structured summary fields for a parent legal category.
/// Persisted to the <c>case_summary_templates</c> collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class CaseSummaryTemplateModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Matches one of the parent incident category codes:
    /// DOWER_MAINTENANCE, DOWRY_DOMESTIC_VIOLENCE, LABOUR_WAGE, LAND_PROPERTY, WOMEN_CHILD_PROTECTION, OTHER
    /// </summary>
    [BsonElement("parentCategoryCode")]
    public string ParentCategoryCode { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("nameEn")]
    public string NameEn { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("fields")]
    public List<CaseSummaryTemplateField> Fields { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single field configuration within a case summary template.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class CaseSummaryTemplateField
{
    /// <summary>
    /// Stable canonical key (e.g. "applicant", "dowerAmount", "unpaidWageAmount", "safetyRisk").
    /// Used for matching against conversation answers and extracted case facts.
    /// </summary>
    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Bangla label displayed on paralegal desk and reports.</summary>
    [BsonElement("labelBn")]
    public string LabelBn { get; set; } = string.Empty;

    /// <summary>Optional English label.</summary>
    [BsonElement("labelEn")]
    [BsonIgnoreIfNull]
    public string? LabelEn { get; set; }

    /// <summary>Display and export ordering sequence.</summary>
    [BsonElement("order")]
    public int Order { get; set; }

    /// <summary>Specifies if this field is legally required for case evaluation.</summary>
    [BsonElement("isRequired")]
    public bool IsRequired { get; set; }

    /// <summary>Data format type: "Text", "Amount", "Date", "Choice", "EvidenceList".</summary>
    [BsonElement("fieldType")]
    public string FieldType { get; set; } = "Text";

    /// <summary>
    /// Known question codes and extracted fact aliases that map into this field
    /// (e.g. ["INCIDENT_VICTIM", "applicant", "VICTIM_NAME"]).
    /// </summary>
    [BsonElement("sourceQuestionCodes")]
    public List<string> SourceQuestionCodes { get; set; } = [];

    /// <summary>Guidance on how this field may be inferred if not explicitly stated.</summary>
    [BsonElement("defaultInferenceNote")]
    [BsonIgnoreIfNull]
    public string? DefaultInferenceNote { get; set; }
}
