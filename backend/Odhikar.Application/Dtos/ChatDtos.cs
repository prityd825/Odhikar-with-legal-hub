using Microsoft.AspNetCore.Http;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

/// <summary>
/// Unified request for a chat turn containing text or audio.
/// Can be submitted via multipart/form-data.
/// </summary>
public sealed class ChatRequest
{
    public Guid? ConversationId { get; set; }
    public string? IncidentType { get; set; }
    public string? CategoryCode { get; set; }
    public string? Text { get; set; }
    public IFormFile? Audio { get; set; }
}

/// <summary>
/// JSON-based request for text-only turns.
/// </summary>
public sealed record ChatTextRequest(
    Guid? ConversationId,
    string? IncidentType,
    string Text,
    string? CategoryCode = null
);

/// <summary>
/// Structured result of the user's incoming turn/message.
/// </summary>
public sealed record UserMessageResultDto(
    string Text,
    string InputType,
    string? AudioUrl,
    int SequenceNumber,
    IReadOnlyList<string> ImportantFactsMentioned,
    IReadOnlyList<string> NewInformationProvided,
    DateTime Timestamp
);

/// <summary>
/// Safety alert and emergency escalation payload for frontends.
/// </summary>
public sealed record AlertInformationDto(
    bool IsActive,
    string? AlertType,
    string? Severity,
    string? Title,
    string? Message,
    string? MatchedSignal,
    string? Helpline
);

/// <summary>
/// Structured follow-up question information.
/// </summary>
public sealed record NextQuestionDto(
    string? Text,
    string? CategoryId,
    string? CategoryName,
    string? QuestionId,
    bool IsFollowUp
);

/// <summary>
/// Detailed conversation completion and readiness state.
/// </summary>
public sealed record ConversationCompletionStatusDto(
    bool IsComplete,
    string? CompletionReason,
    string? Stage,
    int ReadinessPercentage,
    int TotalTurns,
    bool IsAwaitingInput
);

/// <summary>
/// Unified response returned after processing a turn.
/// </summary>
public sealed record ChatResponse(
    Guid ConversationId,
    string AssistantMessage,
    string? UserTranscript,
    string InputMessageType,
    bool IsFollowUpQuestion,
    bool IsComplete,
    bool IsSafetyEscalated,
    string? SafetyReason,
    string Category,
    ExtractedLegalData? ExtractedData,
    DateTime Timestamp,
    string? FollowUpQuestion = null,
    OpenRouterAnalysisResult? Analysis = null,
    ConversationState? State = null,
    FinalConversationReport? FinalReport = null,
    string? UserMessage = null,
    UserMessageResultDto? UserMessageResult = null,
    AlertInformationDto? AlertInformation = null,
    string? NextFollowUpQuestion = null,
    NextQuestionDto? NextQuestion = null,
    string? Status = null,
    string? ConversationStatus = null,
    string? CurrentStage = null,
    ConversationCompletionStatusDto? CompletionStatus = null,
    string? CompletionReason = null,
    string? CaseId = null,
    string? CaseNumber = null,
    GeneratedCaseSummaryDto? GeneratedCaseSummary = null,
    GeneratedApplicationDto? GeneratedApplication = null
);

/// <summary>
/// Message item representation for conversation history.
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    string Role,
    string MessageType,
    string Text,
    string? AudioUrl,
    DateTime Timestamp,
    string? InputType = null,
    int SequenceNumber = 1
)
{
    public static ChatMessageDto From(ConversationMessage msg) => new(
        msg.Id,
        msg.Role,
        msg.MessageType,
        msg.Text,
        msg.AudioUrl,
        msg.Timestamp,
        msg.InputType,
        msg.SequenceNumber
    );
}

/// <summary>
/// Full conversation history and metadata.
/// </summary>
public sealed record ConversationDetailDto(
    Guid Id,
    string IncidentType,
    string Status,
    bool IsComplete,
    bool IsSafetyEscalated,
    string? SafetyReason,
    string Category,
    ExtractedLegalData? ExtractedData,
    IReadOnlyList<ChatMessageDto> Messages,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ConversationState? State = null,
    AIAnalysis? Analysis = null
)
{
    public static ConversationDetailDto From(ConversationModel model) => new(
        model.Id,
        model.IncidentType,
        model.Status,
        model.IsComplete,
        model.IsSafetyEscalated,
        model.SafetyReason,
        model.Category,
        model.ExtractedData,
        model.Messages.Select(ChatMessageDto.From).ToList(),
        model.CreatedAt,
        model.UpdatedAt,
        model.State,
        model.Analysis
    );
}
