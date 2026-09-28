using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Service contract for managing case summary templates and dynamically
/// generating structured case summaries from cases or conversation turns.
/// </summary>
public interface ICaseSummaryTemplateService
{
    Task<IReadOnlyList<CaseSummaryTemplateDto>> GetTemplatesAsync(bool activeOnly = true, CancellationToken ct = default);

    Task<CaseSummaryTemplateDto?> GetTemplateByParentCategoryAsync(string parentCategoryCode, CancellationToken ct = default);

    Task<CaseSummaryTemplateDto?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default);

    Task<CaseSummaryResultDto?> BuildSummaryFromCaseAsync(CaseModel caseModel, CancellationToken ct = default);

    Task<CaseSummaryResultDto?> BuildSummaryFromConversationAsync(Conversation conversation, CancellationToken ct = default);
}
