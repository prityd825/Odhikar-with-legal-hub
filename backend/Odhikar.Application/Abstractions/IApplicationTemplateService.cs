using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

public interface IApplicationTemplateService
{
    Task<IReadOnlyList<ApplicationTemplateDto>> GetTemplatesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<ApplicationTemplateDto?> GetTemplateByCategoryAsync(string categoryCode, CancellationToken ct = default);
    Task<ApplicationTemplateDto?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default);

    Task<GeneratedApplicationDto> GenerateDraftForConversationAsync(
        Guid conversationId,
        GenerateApplicationRequest? request = null,
        CancellationToken ct = default);

    Task<GeneratedApplicationDto?> GetGeneratedApplicationByConversationAsync(
        Guid conversationId,
        CancellationToken ct = default);

    Task<GeneratedApplicationDto?> GetGeneratedApplicationByIdAsync(
        Guid id,
        CancellationToken ct = default);

    Task<GeneratedApplicationDto?> UpdateReviewStatusAsync(
        Guid id,
        UpdateApplicationReviewStatusRequest request,
        CancellationToken ct = default);

    Task<GeneratedApplicationDto?> GetGeneratedApplicationByCaseIdAsync(
        Guid caseId,
        CancellationToken ct = default);

    Task<GeneratedApplicationDto> GenerateDraftForCaseAsync(
        Guid caseId,
        GenerateApplicationRequest? request = null,
        CancellationToken ct = default);

    Task<ApplicationPdfDocumentModel> BuildApplicationPdfModelForCaseAsync(
        CaseModel @case,
        CancellationToken ct = default);

    Task EnsureIndexesAsync(CancellationToken ct = default);
}
