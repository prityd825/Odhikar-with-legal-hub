using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Domain model for high-level parent incident categories.
/// Groups granular incident types (e.g. DOWRY_DEMAND under DOWRY_DOMESTIC_VIOLENCE).
/// Persisted in the MongoDB collection <c>parent_incident_categories</c>.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ParentIncidentCategoryModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique machine code, e.g. "DOWER_MAINTENANCE", "DOWRY_DOMESTIC_VIOLENCE".</summary>
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Bengali display title, e.g. "দেনমোহর / ভরণপোষণ".</summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>English title for developer and cross-cultural reference.</summary>
    [BsonElement("nameEn")]
    [BsonIgnoreIfNull]
    public string? NameEn { get; set; }

    /// <summary>Bengali description or scope statement.</summary>
    [BsonElement("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; set; }

    /// <summary>Optional UI icon identifier.</summary>
    [BsonElement("icon")]
    [BsonIgnoreIfNull]
    public string? Icon { get; set; }

    /// <summary>Presentation sorting order (1 to 6).</summary>
    [BsonElement("order")]
    public int Order { get; set; } = 1;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
