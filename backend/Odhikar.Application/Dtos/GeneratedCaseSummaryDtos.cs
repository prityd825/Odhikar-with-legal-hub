using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

/// <summary>
/// DTO representing an individual structured field in a generated case summary.
/// </summary>
public sealed record GeneratedCaseSummaryFieldDto(
    string Key,
    string LabelBn,
    string? LabelEn,
    int Order,
    bool IsRequired,
    string FieldType,
    string Value,
    string Status,
    bool IsInferred,
    string? InferenceNote,
    double? Confidence,
    string? SourceKey
)
{
    public static GeneratedCaseSummaryFieldDto From(GeneratedCaseSummaryField field) =>
        new(
            field.Key,
            field.LabelBn,
            field.LabelEn,
            field.Order,
            field.IsRequired,
            field.FieldType,
            field.Value,
            field.Status,
            field.IsInferred,
            field.InferenceNote,
            field.Confidence,
            field.SourceKey
        );
}

/// <summary>
/// DTO representing a generated case summary persisted in MongoDB.
/// </summary>
public sealed record GeneratedCaseSummaryDto(
    Guid Id,
    Guid ConversationId,
    string IncidentCategoryCode,
    string? IncidentTypeCode,
    string TemplateCode,
    int TemplateVersion,
    IReadOnlyList<GeneratedCaseSummaryFieldDto> StructuredContent,
    string GeneratedText,
    string Status,
    double CompletionRate,
    int TotalRequiredFields,
    int CompletedRequiredFields,
    IReadOnlyList<string> MissingRequiredFields,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static GeneratedCaseSummaryDto From(GeneratedCaseSummaryModel model) =>
        new(
            model.Id,
            model.ConversationId,
            model.IncidentCategoryCode,
            model.IncidentTypeCode,
            model.TemplateCode,
            model.TemplateVersion,
            model.StructuredContent.Select(GeneratedCaseSummaryFieldDto.From).OrderBy(f => f.Order).ToList(),
            model.GeneratedText,
            model.Status,
            model.CompletionRate,
            model.TotalRequiredFields,
            model.CompletedRequiredFields,
            model.MissingRequiredFields,
            model.CreatedAt,
            model.UpdatedAt
        );
}

/// <summary>
/// Request to update the workflow status of a generated summary.
/// </summary>
public sealed class UpdateSummaryStatusRequest
{
    public required string Status { get; set; }
}
