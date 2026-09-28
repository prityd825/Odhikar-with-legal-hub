namespace Odhikar.Application.Dtos;

/// <summary>
/// Request DTO for analyzing conversation text using OpenRouter LLM service.
/// </summary>
public sealed record OpenRouterAnalysisRequest(
    string ConversationText,
    string? IncidentType = null,
    IReadOnlyList<ChatMessageDto>? History = null
);
