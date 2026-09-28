using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Domain model for legal incident types persisted in MongoDB collection <c>incident_types</c>.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class IncidentTypeModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Unique machine code, e.g. "DOWRY_DEMAND".</summary>
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Bengali title of the incident type.</summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>English translation/description for reference.</summary>
    [BsonElement("nameEn")]
    [BsonIgnoreIfNull]
    public string? NameEn { get; set; }

    /// <summary>Associated legal category constant from <see cref="LegalCategories"/>.</summary>
    [BsonElement("category")]
    public string Category { get; set; } = LegalCategories.Unknown;

    /// <summary>Code of the parent incident category (e.g. "DOWRY_DOMESTIC_VIOLENCE").</summary>
    [BsonElement("parentCategoryCode")]
    [BsonIgnoreIfNull]
    public string? ParentCategoryCode { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
