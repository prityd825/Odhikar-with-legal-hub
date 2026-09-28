using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Service contract for saving and retrieving normalized answers keyed by stable question codes.
/// </summary>
public interface IConversationAnswerService
{
    Task SaveAnswerAsync(ConversationAnswerModel answer, CancellationToken ct = default);

    Task SaveAnswersAsync(IEnumerable<ConversationAnswerModel> answers, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationAnswerDto>> GetAnswersByConversationIdAsync(Guid conversationId, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationAnswerDto>> GetAnswersBySessionIdAsync(Guid sessionId, CancellationToken ct = default);

    Task<IReadOnlyList<ConversationAnswerDto>> GetAnswersByCaseIdAsync(Guid caseId, CancellationToken ct = default);

    Task<Dictionary<string, string>> GetAnswerDictionaryAsync(Guid? conversationId, Guid? sessionId, Guid? caseId, CancellationToken ct = default);

    Task LinkCaseIdAsync(Guid sessionId, Guid caseId, CancellationToken ct = default);

    Task EnsureIndexesAsync(CancellationToken ct = default);
}
