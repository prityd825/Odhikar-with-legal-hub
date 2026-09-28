using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>Embedded record of the legal-aid clinic a case has been referred to.</summary>
[BsonIgnoreExtraElements]
public sealed class ClinicReferral
{
    [BsonElement("clinicId")]
    public string ClinicId { get; set; } = string.Empty;

    [BsonElement("clinicName")]
    public string ClinicName { get; set; } = string.Empty;

    [BsonElement("scheduledSlot")]
    public DateTime ScheduledSlot { get; set; }
}
