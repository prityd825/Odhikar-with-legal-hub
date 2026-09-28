using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Result structure returned from Gemini conversational reasoning.
/// </summary>
public sealed record GeminiConversationResult(
    string AssistantMessage,
    bool IsFollowUpQuestion,
    bool IsComplete,
    bool IsSafetyEscalated,
    string? SafetyReason,
    string Category,
    ExtractedLegalData? ExtractedData,
    string? FollowUpQuestion = null
);

/// <summary>
/// Service abstraction for Google Gemini API capabilities:
/// - Multimodal speech-to-text audio transcription
/// - Multi-turn conversational intelligence with follow-up logic
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Transcribes Bangla or Banglish audio stream using Gemini multimodal capabilities.
    /// </summary>
    Task<string> TranscribeAudioAsync(Stream audioStream, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Generates the next conversational AI response taking into account the conversation history
    /// and the 16 conversational assistant rules.
    /// </summary>
    Task<GeminiConversationResult> GenerateConversationalResponseAsync(
        string incidentType,
        IReadOnlyList<ConversationMessage> history,
        string currentInput,
        CancellationToken ct = default);
}
