using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>A legal-aid clinic that cases can be referred to, persisted to <c>clinics</c>.</summary>
[BsonIgnoreExtraElements]
public sealed class ClinicModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("district")]
    public string District { get; set; } = string.Empty;

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("availableSlots")]
    public List<DateTime> AvailableSlots { get; set; } = new();
}
