using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Service contract for analyzing legal aid intake conversations using OpenRouter LLM APIs.
/// Produces structured classification, extracted information, missing information,
/// suggested next question category, and conversation completion status.
/// </summary>
public interface IOpenRouterLlmService
{
    /// <summary>
    /// Analyzes a conversation turn or transcript, returning a thoroughly validated analysis result.
    /// </summary>
    Task<OpenRouterAnalysisResult> AnalyzeConversationAsync(
        string conversationText,
        string? incidentType = null,
        IReadOnlyList<ConversationMessage>? history = null,
        ConversationState? state = null,
        CancellationToken ct = default);

    /// <summary>
    /// Analyzes a strongly-typed analysis request.
    /// </summary>
    Task<OpenRouterAnalysisResult> AnalyzeTurnAsync(
        OpenRouterAnalysisRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a comprehensive, structured Final Conversation Report based strictly on
    /// the full conversation messages, conversation state, and historical AI analyses.
    /// </summary>
    Task<FinalConversationReport> GenerateFinalReportAsync(
        ConversationModel conversation,
        ConversationState state,
        IReadOnlyList<AIAnalysis> analyses,
        CancellationToken ct = default);
}
