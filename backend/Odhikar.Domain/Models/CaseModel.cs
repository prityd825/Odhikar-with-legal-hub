using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Aggregate root for a single legal-aid intake case, persisted to the
/// <c>cases</c> collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class CaseModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-friendly case reference, e.g. "ODH-2026-0417".</summary>
    [BsonElement("caseNumber")]
    public string CaseNumber { get; set; } = string.Empty;

    /// <summary>One of <see cref="LegalCategories"/>.</summary>
    [BsonElement("category")]
    public string Category { get; set; } = LegalCategories.Unknown;

    [BsonElement("categoryConfidence")]
    public double CategoryConfidence { get; set; }

    [BsonElement("isSafetyEscalated")]
    public bool IsSafetyEscalated { get; set; }

    [BsonElement("safetyReason")]
    [BsonIgnoreIfNull]
    public string? SafetyReason { get; set; }

    /// <summary>
    /// Structured facts keyed by field name (applicant, respondent, marriageDate,
    /// dowerAmount, claimedAmount, evidenceList, ...). Each carries its own
    /// <c>isInferred</c> flag via <see cref="ExtractedField"/>.
    /// </summary>
    [BsonElement("extractedData")]
    public Dictionary<string, ExtractedField> ExtractedData { get; set; } = new();

    [BsonElement("dialogueHistory")]
    public List<DialogueTurn> DialogueHistory { get; set; } = new();

    [BsonElement("draftComplaintText")]
    [BsonIgnoreIfNull]
    public string? DraftComplaintText { get; set; }

    [BsonElement("clinicReferral")]
    [BsonIgnoreIfNull]
    public ClinicReferral? ClinicReferral { get; set; }

    /// <summary>One of <see cref="CaseStatuses"/>.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = CaseStatuses.New;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
