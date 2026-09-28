using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Result of the backend's question category resolution and question flow decision.
/// </summary>
public sealed record QuestionCategoryDecision(
    string CategoryId,
    string CategoryName,
    string QuestionBangla,
    string SelectionSource,
    bool IsIntakeComplete,
    string? Reasoning = null
);

/// <summary>
/// Extensible service managing question categories and controlling the final follow-up question flow.
/// </summary>
public interface IQuestionCategoryService
{
    /// <summary>Returns all active question categories ordered by priority.</summary>
    IReadOnlyList<QuestionCategoryDefinition> GetAllCategories();

    /// <summary>Retrieves a category definition by ID.</summary>
    QuestionCategoryDefinition? GetCategory(string categoryId);

    /// <summary>Returns categories applicable to a specific legal incident type.</summary>
    IReadOnlyList<QuestionCategoryDefinition> GetApplicableCategories(string? incidentType);

    /// <summary>Returns a formatted string of category IDs and descriptions for LLM prompt injection.</summary>
    string GetCategoryPromptDescriptions();

    /// <summary>
    /// Evaluates the LLM's suggested category, validates it against conversation state,
    /// queries the MongoDB Question Bank contextually, and determines the final question.
    /// </summary>
    Task<QuestionCategoryDecision> DecideNextQuestionAsync(
        string? suggestedCategoryCode,
        string? suggestedQuestionBangla,
        ConversationState state,
        string? incidentType,
        ExtractedLegalData? extractedData,
        string? userContextText = null,
        CancellationToken ct = default);
}
