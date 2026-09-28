using Microsoft.AspNetCore.Http;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Orchestrates conversation lifecycles, persists turns, validates voice and text inputs,
/// and queries the Google Gemini conversational AI service.
/// </summary>
public sealed class ConversationService(
    IConversationRepository repository,
    IGeminiService gemini,
    ILogger<ConversationService> logger) : IConversationService
{
    private readonly IConversationRepository _repository = repository;
    private readonly IGeminiService _gemini = gemini;
    private readonly ILogger<ConversationService> _logger = logger;

    private const long MaxAudioSizeBytes = 25 * 1024 * 1024; // 25 MB cap

    public async Task<ChatResponse> ProcessTurnAsync(
        Guid? conversationId,
        string? incidentType,
        string? text,
        IFormFile? audio,
        CancellationToken ct = default)
    {
        var hasText = !string.IsNullOrWhiteSpace(text);
        var hasAudio = audio != null && audio.Length > 0;

        if (!hasText && !hasAudio)
        {
            throw new ArgumentException("টেক্সট অথবা অডিও ইনপুট আবশ্যক। দুটি একসাথে খালি রাখা যাবে না।");
        }

        string inputMessageType;
        string userText;
        string? audioReference = null;

        // 1. Process Voice Input via Google Gemini Multimodal Audio
        if (hasAudio)
        {
            if (audio!.Length > MaxAudioSizeBytes)
            {
                throw new ArgumentException("অডিও ফাইলের সাইজ ২৫ মেগাবাইটের বেশি হতে পারবে না।");
            }

            _logger.LogInformation("Processing incoming voice input: {FileName}, size {Bytes} bytes, content-type {Type}",
                audio.FileName, audio.Length, audio.ContentType);

            await using var audioStream = audio.OpenReadStream();
            var transcript = await _gemini.TranscribeAudioAsync(audioStream, audio.ContentType, ct);

            if (string.IsNullOrWhiteSpace(transcript) && !string.IsNullOrWhiteSpace(text))
            {
                _logger.LogInformation("Gemini audio transcription was empty, falling back to client-provided voice text: '{Text}'", text);
                transcript = text.Trim();
            }

            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogWarning("Audio transcription produced empty text and no client fallback text was provided.");
                // Return immediate clarification turn without breaking conversation
                var activeConversation = await GetOrCreateConversationAsync(conversationId, incidentType, ct);

                return new ChatResponse(
                    ConversationId: activeConversation.Id,
                    AssistantMessage: "আপনার অডিওটি স্পষ্টভাবে শোনা যায়নি বা কোনো কথা পাওয়া যায়নি। অনুগ্রহ করে মাইক বাটনে চেপে আবার পরিষ্কারভাবে বলুন।",
                    UserTranscript: string.Empty,
                    InputMessageType: "Voice",
                    IsFollowUpQuestion: true,
                    IsComplete: false,
                    IsSafetyEscalated: false,
                    SafetyReason: null,
                    Category: activeConversation.Category,
                    ExtractedData: activeConversation.ExtractedData,
                    Timestamp: DateTime.UtcNow,
                    FollowUpQuestion: "অনুগ্রহ করে মাইক বাটনে চেপে আবার পরিষ্কারভাবে বলুন।",
                    Analysis: null,
                    State: activeConversation.State,
                    FinalReport: null,
                    UserMessage: string.Empty,
                    UserMessageResult: new UserMessageResultDto(
                        Text: string.Empty,
                        InputType: "Voice",
                        AudioUrl: null,
                        SequenceNumber: activeConversation.Messages.Count + 1,
                        ImportantFactsMentioned: Array.Empty<string>(),
                        NewInformationProvided: Array.Empty<string>(),
                        Timestamp: DateTime.UtcNow
                    ),
                    AlertInformation: new AlertInformationDto(
                        IsActive: false,
                        AlertType: "None",
                        Severity: "Normal",
                        Title: null,
                        Message: null,
                        MatchedSignal: null,
                        Helpline: null
                    ),
                    NextFollowUpQuestion: "অনুগ্রহ করে মাইক বাটনে চেপে আবার পরিষ্কারভাবে বলুন।",
                    NextQuestion: new NextQuestionDto(
                        Text: "অনুগ্রহ করে মাইক বাটনে চেপে আবার পরিষ্কারভাবে বলুন।",
                        CategoryId: "clarification",
                        CategoryName: "অডিও স্পষ্টীকরণ",
                        QuestionId: null,
                        IsFollowUp: true
                    ),
                    Status: activeConversation.Status,
                    ConversationStatus: activeConversation.Status,
                    CurrentStage: activeConversation.State?.CurrentStage ?? "DetailGathering",
                    CompletionStatus: new ConversationCompletionStatusDto(
                        IsComplete: false,
                        CompletionReason: null,
                        Stage: activeConversation.State?.CurrentStage ?? "DetailGathering",
                        ReadinessPercentage: activeConversation.State?.ReadinessPercentage ?? 0,
                        TotalTurns: activeConversation.State?.TurnCount ?? 0,
                        IsAwaitingInput: true
                    ),
                    CompletionReason: null
                );
            }

            inputMessageType = "Voice";
            userText = transcript.Trim();
            audioReference = $"audio://conversations/{conversationId?.ToString() ?? "new"}/{audio.FileName}";
        }
        else
        {
            inputMessageType = "Text";
            userText = text!.Trim();
        }

        // 2. Load or Create Conversation
        var conversation = await GetOrCreateConversationAsync(conversationId, incidentType, ct);

        // 3. Save User Message
        var userMessage = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "User",
            MessageType = inputMessageType,
            Text = userText,
            AudioUrl = audioReference,
            Timestamp = DateTime.UtcNow
        };

        conversation.Messages.Add(userMessage);

        _logger.LogInformation("Conversation {ConversationId} Turn: Role=User, Type={Type}, Content length={Len}",
            conversation.Id, inputMessageType, userText.Length);

        // 4. Retrieve recent history for context window (last 14 messages = ~7 full turns)
        var priorHistory = conversation.Messages
            .Where(m => m.Id != userMessage.Id)
            .TakeLast(14)
            .ToList();

        // 5. Query Google Gemini Conversational Intelligence
        var geminiResult = await _gemini.GenerateConversationalResponseAsync(
            conversation.IncidentType,
            priorHistory,
            userText,
            ct);

        // 6. Save Assistant Response Message
        var assistantMessage = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "Assistant",
            MessageType = "Text",
            Text = geminiResult.AssistantMessage,
            AudioUrl = null,
            Timestamp = DateTime.UtcNow
        };

        conversation.Messages.Add(assistantMessage);

        // 7. Update Conversation State Flags & Entities
        conversation.IsComplete = geminiResult.IsComplete;
        conversation.IsSafetyEscalated = geminiResult.IsSafetyEscalated;
        if (!string.IsNullOrWhiteSpace(geminiResult.SafetyReason))
        {
            conversation.SafetyReason = geminiResult.SafetyReason;
        }
        if (!string.IsNullOrWhiteSpace(geminiResult.Category))
        {
            conversation.Category = geminiResult.Category;
        }
        if (geminiResult.ExtractedData != null)
        {
            conversation.ExtractedData = geminiResult.ExtractedData;
        }

        conversation.Status = conversation.IsSafetyEscalated
            ? "SafetyEscalated"
            : (conversation.IsComplete ? "Completed" : "Active");

        await _repository.UpdateConversationAsync(conversation, ct);

        _logger.LogInformation("Conversation {ConversationId} Turn Complete: Assistant replied, IsFollowUp={FollowUp}, Complete={Complete}",
            conversation.Id, geminiResult.IsFollowUpQuestion, geminiResult.IsComplete);

        var isComplete = geminiResult.IsComplete;
        var alertInfo = new AlertInformationDto(
            IsActive: geminiResult.IsSafetyEscalated,
            AlertType: geminiResult.IsSafetyEscalated ? "SafetyEmergency" : "None",
            Severity: geminiResult.IsSafetyEscalated ? "Critical" : "Normal",
            Title: geminiResult.IsSafetyEscalated ? "জরুরি নিরাপত্তা সতর্কতা" : null,
            Message: geminiResult.SafetyReason,
            MatchedSignal: null,
            Helpline: geminiResult.IsSafetyEscalated ? "৯৯৯, ১০৯" : null
        );

        var userMessageResult = new UserMessageResultDto(
            Text: userText,
            InputType: inputMessageType,
            AudioUrl: null,
            SequenceNumber: conversation.Messages.Count(m => m.Role == "User"),
            ImportantFactsMentioned: Array.Empty<string>(),
            NewInformationProvided: Array.Empty<string>(),
            Timestamp: DateTime.UtcNow
        );

        var nextQuestionDto = !isComplete && !string.IsNullOrWhiteSpace(geminiResult.FollowUpQuestion)
            ? new NextQuestionDto(geminiResult.FollowUpQuestion, null, null, null, geminiResult.IsFollowUpQuestion)
            : null;

        return new ChatResponse(
            ConversationId: conversation.Id,
            AssistantMessage: geminiResult.AssistantMessage,
            UserTranscript: inputMessageType == "Voice" ? userText : null,
            InputMessageType: inputMessageType,
            IsFollowUpQuestion: geminiResult.IsFollowUpQuestion,
            IsComplete: isComplete,
            IsSafetyEscalated: geminiResult.IsSafetyEscalated,
            SafetyReason: geminiResult.SafetyReason,
            Category: conversation.Category,
            ExtractedData: conversation.ExtractedData,
            Timestamp: DateTime.UtcNow,
            FollowUpQuestion: geminiResult.FollowUpQuestion,
            Analysis: null,
            State: conversation.State,
            FinalReport: null,
            UserMessage: userText,
            UserMessageResult: userMessageResult,
            AlertInformation: alertInfo,
            NextFollowUpQuestion: geminiResult.FollowUpQuestion,
            NextQuestion: nextQuestionDto,
            Status: conversation.Status,
            ConversationStatus: conversation.Status,
            CurrentStage: conversation.State?.CurrentStage ?? (isComplete ? "Completed" : "DetailGathering"),
            CompletionStatus: new ConversationCompletionStatusDto(
                IsComplete: isComplete,
                CompletionReason: isComplete ? "তথ্য সংগ্রহ সম্পন্ন হয়েছে" : null,
                Stage: conversation.State?.CurrentStage ?? (isComplete ? "Completed" : "DetailGathering"),
                ReadinessPercentage: conversation.State?.ReadinessPercentage ?? (isComplete ? 100 : 50),
                TotalTurns: conversation.Messages.Count(m => m.Role == "User"),
                IsAwaitingInput: !isComplete
            ),
            CompletionReason: isComplete ? "তথ্য সংগ্রহ সম্পন্ন হয়েছে" : null
        );
    }

    public async Task<ConversationDetailDto?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _repository.GetByIdAsync(conversationId, ct);
        return conversation is null ? null : ConversationDetailDto.From(conversation);
    }

    private async Task<ConversationModel> GetOrCreateConversationAsync(
        Guid? conversationId,
        string? incidentType,
        CancellationToken ct)
    {
        if (conversationId.HasValue && conversationId.Value != Guid.Empty)
        {
            var existing = await _repository.GetByIdAsync(conversationId.Value, ct);
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(incidentType) && existing.IncidentType == "সাধারণ আইনি সমস্যা")
                {
                    existing.IncidentType = incidentType;
                }
                return existing;
            }
        }

        var newConv = new ConversationModel
        {
            Id = conversationId.HasValue && conversationId.Value != Guid.Empty
                ? conversationId.Value
                : Guid.NewGuid(),
            IncidentType = string.IsNullOrWhiteSpace(incidentType) ? "সাধারণ আইনি সমস্যা" : incidentType.Trim(),
            Category = string.IsNullOrWhiteSpace(incidentType) ? "সাধারণ আইনি সমস্যা" : incidentType.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(newConv, ct);
        return newConv;
    }
}
