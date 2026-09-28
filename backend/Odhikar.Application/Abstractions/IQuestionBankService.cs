using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Service managing the MongoDB-based Question Bank and context-driven question selection.
/// </summary>
public interface IQuestionBankService
{
    /// <summary>Returns all active questions in the question bank.</summary>
    Task<IReadOnlyList<QuestionBankItem>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns active questions for a given category, optionally filtered by incident type.</summary>
    Task<IReadOnlyList<QuestionBankItem>> GetByCategoryAsync(string category, string? incidentType = null, CancellationToken ct = default);

    /// <summary>Returns active questions mapped to a parent incident category code (e.g. "DOWER_MAINTENANCE", "DOWRY_DOMESTIC_VIOLENCE").</summary>
    Task<IReadOnlyList<QuestionBankItem>> GetByParentCategoryAsync(string parentCategoryCode, CancellationToken ct = default);

    /// <summary>Retrieves a question entry by its QuestionId (e.g. "Q-DOWER-001").</summary>
    Task<QuestionBankItem?> GetByQuestionIdAsync(string questionId, CancellationToken ct = default);

    /// <summary>
    /// Selects the most contextually relevant, unasked question for a category and conversation state.
    /// </summary>
    Task<QuestionBankItem?> SelectContextualQuestionAsync(
        string category,
        ConversationState state,
        string? incidentType,
        string? userContextText,
        CancellationToken ct = default);

    /// <summary>Adds a new question to the bank.</summary>
    Task<QuestionBankItem> AddQuestionAsync(QuestionBankItem item, CancellationToken ct = default);
}
