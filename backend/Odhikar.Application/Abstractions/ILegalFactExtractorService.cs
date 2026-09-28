using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Domain service responsible for extracting structured legal facts from conversational questions & answers,
/// converting Bengali numerals and entities, updating conversation state, and persisting normalized answers.
/// </summary>
public interface ILegalFactExtractorService
{
    /// <summary>
    /// Extracts legal facts from the current turn's context (target question asked, user answer transcript, LLM analysis),
    /// persists them to conversation_answers, and updates conversation state and extracted data.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ExtractAndPersistFactsAsync(
        Conversation conversation,
        string userText,
        string inputMessageType,
        OpenRouterAnalysisResult? openRouterResult,
        CancellationToken ct = default);
}
