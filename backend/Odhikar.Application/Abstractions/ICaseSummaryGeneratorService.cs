using Odhikar.Application.Dtos;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Service contract for generating, persisting, and retrieving structured case summaries from conversations.
/// </summary>
public interface ICaseSummaryGeneratorService
{
    Task<GeneratedCaseSummaryDto> GenerateSummaryAsync(Guid conversationId, CancellationToken ct = default);

    Task<GeneratedCaseSummaryDto?> GetSummaryByConversationIdAsync(Guid conversationId, CancellationToken ct = default);

    Task<GeneratedCaseSummaryDto?> GetSummaryByIdAsync(Guid summaryId, CancellationToken ct = default);

    Task<GeneratedCaseSummaryDto?> UpdateSummaryStatusAsync(Guid summaryId, string newStatus, CancellationToken ct = default);

    Task UpdateSummaryFieldsAsync(Guid conversationId, IDictionary<string, string> fields, CancellationToken ct = default);

    Task EnsureIndexesAsync(CancellationToken ct = default);
}
