using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

public sealed record ApplicationTemplateDto(
    Guid Id,
    string CategoryCode,
    string TitleBn,
    string TitleEn,
    string RecipientAuthorityBn,
    string SubjectBn,
    string BodyTemplateBn,
    IReadOnlyList<ApplicationPlaceholderDefinitionDto> SupportedPlaceholders,
    string DisclaimerNoticeBn,
    int Version,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ApplicationTemplateDto From(ApplicationTemplateModel model) =>
        new(
            model.Id,
            model.CategoryCode,
            model.TitleBn,
            model.TitleEn,
            model.RecipientAuthorityBn,
            model.SubjectBn,
            model.BodyTemplateBn,
            model.SupportedPlaceholders.Select(ApplicationPlaceholderDefinitionDto.From).ToList(),
            model.DisclaimerNoticeBn,
            model.Version,
            model.IsActive,
            model.CreatedAt,
            model.UpdatedAt);
}

public sealed record ApplicationPlaceholderDefinitionDto(
    string Placeholder,
    string Key,
    string LabelBn,
    string FallbackBn,
    bool IsRequired,
    IReadOnlyList<string> Aliases)
{
    public static ApplicationPlaceholderDefinitionDto From(ApplicationPlaceholderDefinition def) =>
        new(
            def.Placeholder,
            def.Key,
            def.LabelBn,
            def.FallbackBn,
            def.IsRequired,
            def.Aliases);
}

public sealed record GeneratedApplicationDto(
    Guid Id,
    Guid ConversationId,
    Guid? CaseSummaryId,
    string IncidentCategoryCode,
    string? IncidentTypeCode,
    string TemplateCode,
    string Subject,
    string ApplicationText,
    string Status,
    string ReviewStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? CaseId = null,
    Guid? TemplateId = null,
    string? TemplateTitleBn = null,
    string? RecipientAuthorityBn = null,
    string? DisclaimerNotice = null,
    IReadOnlyDictionary<string, string>? PlaceholdersUsed = null,
    IReadOnlyList<string>? MissingPlaceholders = null,
    string? ReviewedBy = null,
    string? ReviewNotes = null,
    DateTime? ReviewedAt = null)
{
    public static GeneratedApplicationDto From(GeneratedApplicationModel model) =>
        new(
            model.Id,
            model.ConversationId,
            model.CaseSummaryId,
            model.IncidentCategoryCode,
            model.IncidentTypeCode,
            model.TemplateCode,
            model.Subject,
            model.ApplicationText,
            model.Status,
            model.ReviewStatus,
            model.CreatedAt,
            model.UpdatedAt,
            model.CaseId,
            model.TemplateId,
            model.TemplateTitleBn,
            model.RecipientAuthorityBn,
            model.DisclaimerNotice,
            model.PlaceholdersUsed,
            model.MissingPlaceholders,
            model.ReviewedBy,
            model.ReviewNotes,
            model.ReviewedAt);
}

public sealed record GenerateApplicationRequest(
    string? TargetCategoryCode = null,
    Dictionary<string, string>? ExplicitOverrides = null);

public sealed record UpdateApplicationReviewStatusRequest(
    string? Status = null,
    string? ReviewStatus = null,
    string? ReviewedBy = null,
    string? ReviewNotes = null);
