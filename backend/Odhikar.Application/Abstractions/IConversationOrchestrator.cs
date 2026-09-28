using Microsoft.AspNetCore.Http;
using Odhikar.Application.Dtos;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Central coordinator for the conversational legal intake flow.
/// Coordinates user voice/text message ingestion, alarming content detection & safety escalation,
/// message persistence, legal AI classification, conversational state management,
/// dynamic next follow-up question selection, and automated case creation with legal draft complaints.
/// </summary>
public interface IConversationOrchestrator
{
    /// <summary>
    /// Processes a single multi-turn dialogue step from either audio (voice) or typed text.
    /// </summary>
    Task<ChatResponse> ProcessTurnAsync(
        Guid? conversationId,
        string? incidentType,
        string? text,
        IFormFile? audio,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a conversation and its complete chronological turn history.
    /// </summary>
    Task<ConversationDetailDto?> GetConversationAsync(
        Guid conversationId,
        CancellationToken ct = default);
}
