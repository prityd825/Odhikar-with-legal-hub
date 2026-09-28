using Microsoft.AspNetCore.Http;
using Odhikar.Application.Dtos;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Orchestrates conversational AI turns with voice or text input, state persistence,
/// and Google Gemini conversational processing.
/// </summary>
public interface IConversationService
{
    Task<ChatResponse> ProcessTurnAsync(
        Guid? conversationId,
        string? incidentType,
        string? text,
        IFormFile? audio,
        CancellationToken ct = default);

    Task<ConversationDetailDto?> GetConversationAsync(
        Guid conversationId,
        CancellationToken ct = default);
}
