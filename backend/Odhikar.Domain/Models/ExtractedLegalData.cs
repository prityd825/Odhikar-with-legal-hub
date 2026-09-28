namespace Odhikar.Domain.Models;

/// <summary>Extracted field value along with inference metadata.</summary>
public sealed record InferredField(
    string? Value = null,
    bool IsInferred = false
);

/// <summary>Extracted legal data entities from the ongoing intake dialogue.</summary>
public sealed record ExtractedLegalData(
    InferredField? Applicant = null,
    InferredField? Respondent = null,
    InferredField? MarriageOrIncidentDate = null,
    InferredField? DisputedAmount = null,
    List<string>? EvidenceList = null
);
