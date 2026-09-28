using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

/// <summary>
/// Status of an individual field in a generated case summary.
/// </summary>
public static class SummaryFieldStatus
{
    public const string Stated = "Stated";
    public const string Inferred = "Inferred";
    public const string Missing = "Missing";
}

/// <summary>
/// Public DTO representing a case summary template configuration.
/// </summary>
public sealed record CaseSummaryTemplateDto(
    Guid Id,
    string ParentCategoryCode,
    string Name,
    string NameEn,
    string Description,
    int Version,
    bool IsActive,
    IReadOnlyList<CaseSummaryTemplateFieldDto> Fields
)
{
    public static CaseSummaryTemplateDto From(CaseSummaryTemplateModel model) =>
        new(
            model.Id,
            model.ParentCategoryCode,
            model.Name,
            model.NameEn,
            model.Description,
            model.Version,
            model.IsActive,
            model.Fields.Select(CaseSummaryTemplateFieldDto.From).OrderBy(f => f.Order).ToList()
        );
}

/// <summary>
/// Public DTO for a single field specification within a template.
/// </summary>
public sealed record CaseSummaryTemplateFieldDto(
    string Key,
    string LabelBn,
    string? LabelEn,
    int Order,
    bool IsRequired,
    string FieldType,
    IReadOnlyList<string> SourceQuestionCodes,
    string? DefaultInferenceNote
)
{
    public static CaseSummaryTemplateFieldDto From(CaseSummaryTemplateField field) =>
        new(
            field.Key,
            field.LabelBn,
            field.LabelEn,
            field.Order,
            field.IsRequired,
            field.FieldType,
            field.SourceQuestionCodes,
            field.DefaultInferenceNote
        );
}

/// <summary>
/// Result of evaluating a specific field for a case/conversation against the template.
/// Differentiates between actual user-stated values, inferred values (with reasoning & confidence),
/// and missing required/optional information.
/// </summary>
public sealed record CaseSummaryFieldResultDto
{
    public required string Key { get; init; }
    public required string LabelBn { get; init; }
    public string? LabelEn { get; init; }
    public required int Order { get; init; }
    public required bool IsRequired { get; init; }
    public required string FieldType { get; init; }

    /// <summary>
    /// One of: "Stated" (user provided), "Inferred" (derived with confidence), "Missing" (absent).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Actual value stated by user or inferred by the system. Null if missing.</summary>
    public string? Value { get; init; }

    /// <summary>True when the value was inferred rather than explicitly provided.</summary>
    public bool IsInferred { get; init; }

    /// <summary>Explanation note for inferred values.</summary>
    public string? InferenceNote { get; init; }

    /// <summary>Confidence score (0.0 - 1.0) if applicable.</summary>
    public double? Confidence { get; init; }

    /// <summary>The matching question code or extracted fact key that provided this value.</summary>
    public string? SourceKey { get; init; }
}

/// <summary>
/// Complete generated summary for a case or conversation based on its resolved parent category template.
/// </summary>
public sealed record CaseSummaryResultDto
{
    public string? CaseId { get; init; }
    public string? CaseNumber { get; init; }
    public string? ConversationId { get; init; }
    public required string ParentCategoryCode { get; init; }
    public required string ParentCategoryName { get; init; }
    public required int TemplateVersion { get; init; }

    /// <summary>Percentage (0 - 100) of required template fields that have values.</summary>
    public required double CompletionRate { get; init; }

    public required int TotalRequiredFields { get; init; }
    public required int CompletedRequiredFields { get; init; }
    public required int MissingRequiredCount { get; init; }

    /// <summary>Ordered list of all fields evaluated against the template.</summary>
    public required IReadOnlyList<CaseSummaryFieldResultDto> Fields { get; init; }

    /// <summary>List of field keys for required information that is still missing.</summary>
    public required IReadOnlyList<string> MissingRequiredKeys { get; init; }

    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
