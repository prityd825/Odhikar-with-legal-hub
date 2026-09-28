using Microsoft.AspNetCore.Http;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

/// <summary>Request body for a text-based intake turn.</summary>
public sealed record ProcessTextRequest(string? CaseId, string Text);

/// <summary>Request for voice-based intake turn containing audio file and optional case ID.</summary>
public sealed class ProcessVoiceRequest
{
    public required IFormFile Audio { get; set; }
    public string? CaseId { get; set; }
}

/// <summary>Request for voice transcription containing an audio file.</summary>
public sealed class TranscribeRequest
{
    public required IFormFile Audio { get; set; }
}

/// <summary>
/// Response returned to the client after any intake turn. Deliberately compact and
/// client-friendly: exposes only what the voice UI needs, plus the safety breakout flag.
/// </summary>
public sealed record IntakeTurnResponse
{
    public required string CaseId { get; init; }
    public required string CaseNumber { get; init; }
    public required string Category { get; init; }
    public double CategoryConfidence { get; init; }

    /// <summary>The transcribed client input (echoed back for voice turns).</summary>
    public string Transcript { get; init; } = string.Empty;

    /// <summary>The single next question / narrative to speak and display to the client.</summary>
    public string SystemPrompt { get; init; } = string.Empty;

    public bool IsSafetyEscalated { get; init; }
    public string? SafetyReason { get; init; }

    /// <summary>True while the flow still expects more information from the client.</summary>
    public bool AwaitingMoreInfo { get; init; }

    public Dictionary<string, ExtractedField>? ExtractedData { get; init; }

    public static IntakeTurnResponse From(CaseModel c)
    {
        var lastTurn = c.DialogueHistory.LastOrDefault();
        return new IntakeTurnResponse
        {
            CaseId = c.Id.ToString(),
            CaseNumber = c.CaseNumber,
            Category = c.Category,
            CategoryConfidence = c.CategoryConfidence,
            Transcript = lastTurn?.Transcript ?? string.Empty,
            SystemPrompt = lastTurn?.SystemPrompt ?? string.Empty,
            IsSafetyEscalated = c.IsSafetyEscalated,
            SafetyReason = c.SafetyReason,
            AwaitingMoreInfo = !c.IsSafetyEscalated
                               && (c.Category == LegalCategories.Unknown
                                   || string.IsNullOrEmpty(c.DraftComplaintText))
                               && c.Category != LegalCategories.OutOfScope,
            ExtractedData = c.ExtractedData
        };
    }
}

/// <summary>Request to synthesise Bangla speech for the client audio player.</summary>
public sealed record TtsRequest(string Text);

/// <summary>Represents a single chat turn between user and AI model.</summary>
public sealed record ChatTurn(
    string Role,
    string Text
);

/// <summary>Incoming conversational intake request with incident type, current input, and conversation history.</summary>
public sealed record ConversationRequestDto(
    string IncidentType,
    string CurrentInput,
    List<ChatTurn>? History
);

/// <summary>Structured response from Gemini conversational intake turn.</summary>
public sealed record ExtractionResponseDto(
    bool IsSafetyEscalated,
    string? SafetyReason,
    bool IsComplete,
    string Category,
    string? NextFollowUpQuestion,
    ExtractedLegalData? ExtractedData
);
