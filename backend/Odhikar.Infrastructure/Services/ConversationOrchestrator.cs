using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Central coordinator for conversational legal intake.
/// Orchestrates:
/// 1. User message & voice processing (Gemini multimodal STT with Whisper Large v3 fallback)
/// 2. Alarming content detection & emergency safety escalation (2-layer guardrail: keywords + LLM)
/// 3. Message persistence in chronological order
/// 4. AI classification & structured fact extraction
/// 5. Dynamic follow-up question selection within conversation constraints (max 4 turns)
/// 6. Conversation completion, case creation, and pre-approved legal draft rendering.
/// </summary>
public sealed class ConversationOrchestrator(
    IConversationRepository repository,
    IGeminiService gemini,
    IOpenRouterLlmService openRouter,
    ISpeechToTextService asr,
    ISafetyGuardrailService safetyGuardrail,
    ICaseService caseService,
    ITemplateEngineService templateEngine,
    IQuestionCategoryService questionCategoryService,
    IFollowUpQuestionEngine followUpEngine,
    IConversationStateValidator stateValidator,
    IParentCategoryService parentCategoryService,
    ICaseSummaryGeneratorService caseSummaryGenerator,
    IApplicationTemplateService applicationTemplateService,
    IConversationAnswerService answerService,
    ILegalFactExtractorService factExtractor,
    MongoDbContext context,
    ILogger<ConversationOrchestrator> logger) : IConversationOrchestrator, IConversationService
{
    private readonly IConversationRepository _repository = repository;
    private readonly IGeminiService _gemini = gemini;
    private readonly IOpenRouterLlmService _openRouter = openRouter;
    private readonly ISpeechToTextService _asr = asr;
    private readonly ISafetyGuardrailService _safetyGuardrail = safetyGuardrail;
    private readonly ICaseService _caseService = caseService;
    private readonly ITemplateEngineService _templateEngine = templateEngine;
    private readonly IQuestionCategoryService _questionCategoryService = questionCategoryService;
    private readonly IFollowUpQuestionEngine _followUpEngine = followUpEngine;
    private readonly IConversationStateValidator _stateValidator = stateValidator;
    private readonly IParentCategoryService _parentCategoryService = parentCategoryService;
    private readonly ICaseSummaryGeneratorService _caseSummaryGenerator = caseSummaryGenerator;
    private readonly IApplicationTemplateService _applicationTemplateService = applicationTemplateService;
    private readonly IConversationAnswerService _answerService = answerService;
    private readonly ILegalFactExtractorService _factExtractor = factExtractor;
    private readonly MongoDbContext _context = context;
    private readonly ILogger<ConversationOrchestrator> _logger = logger;

    private const long MaxAudioSizeBytes = 25 * 1024 * 1024; // 25 MB cap
    private const int MaxIntakeTurns = 12; // Allow thorough multi-turn intake across all questions in question bank

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

        // -------------------------------------------------------------------------
        // 1. Voice-to-Text & User Message Processing
        // -------------------------------------------------------------------------
        if (hasAudio)
        {
            if (audio!.Length > MaxAudioSizeBytes)
            {
                throw new ArgumentException("অডিও ফাইলের সাইজ ২৫ মেগাবাইটের বেশি হতে পারবে না।");
            }

            _logger.LogInformation("Processing incoming audio: {FileName}, size {Bytes} bytes, content-type {Type}",
                audio.FileName, audio.Length, audio.ContentType);

            string transcript = string.Empty;

            // 1a. Primary: Gemini 1.5 Flash multimodal audio transcription
            try
            {
                await using var stream = audio.OpenReadStream();
                transcript = await _gemini.TranscribeAudioAsync(stream, audio.ContentType, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini audio transcription failed. Attempting fallback...");
            }

            // 1b. Secondary Fallback: Groq Whisper Large v3
            if (string.IsNullOrWhiteSpace(transcript))
            {
                try
                {
                    await using var fallbackStream = audio.OpenReadStream();
                    transcript = await _asr.TranscribeAsync(fallbackStream, audio.FileName, audio.ContentType, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fallback Whisper transcription failed.");
                }
            }

            // 1c. Client-provided text fallback (if speech recognition produced text in the browser)
            if (string.IsNullOrWhiteSpace(transcript))
            {
                if (hasText)
                {
                    _logger.LogInformation("Using client-provided text fallback: '{Text}'", text);
                    transcript = text!.Trim();
                }
            }
            else if (hasText && !string.IsNullOrWhiteSpace(text) && text.Trim().Length > transcript.Length && text.Trim().Contains(transcript))
            {
                _logger.LogInformation("Using richer client-provided text: '{Text}' over audio transcript: '{Transcript}'", text, transcript);
                transcript = text.Trim();
            }

            // 1d. Handle silence or incomprehensible audio gracefully
            if (string.IsNullOrWhiteSpace(transcript))
            {
                _logger.LogWarning("Audio transcription yielded empty transcript.");
                var conv = await GetOrCreateConversationAsync(conversationId, incidentType, ct);

                return new ChatResponse(
                    ConversationId: conv.Id,
                    AssistantMessage: "আপনার কথাটি স্পষ্টভাবে শোনা যায়নি। অনুগ্রহ করে মাইক বাটনে চেপে আবার বলুন অথবা নিচে লিখে জানান।",
                    UserTranscript: string.Empty,
                    InputMessageType: "Voice",
                    IsFollowUpQuestion: true,
                    IsComplete: false,
                    IsSafetyEscalated: false,
                    SafetyReason: null,
                    Category: conv.Category,
                    ExtractedData: conv.ExtractedData,
                    Timestamp: DateTime.UtcNow,
                    FollowUpQuestion: "অনুগ্রহ করে মাইক বাটনে চেপে আবার বলুন অথবা নিচে লিখে জানান।",
                    Analysis: null,
                    State: conv.State,
                    FinalReport: null,
                    UserMessage: string.Empty,
                    UserMessageResult: new UserMessageResultDto(
                        Text: string.Empty,
                        InputType: "Voice",
                        AudioUrl: null,
                        SequenceNumber: conv.Messages.Count + 1,
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
                    NextFollowUpQuestion: "অনুগ্রহ করে মাইক বাটনে চেপে আবার বলুন অথবা নিচে লিখে জানান।",
                    NextQuestion: new NextQuestionDto(
                        Text: "অনুগ্রহ করে মাইক বাটনে চেপে আবার বলুন অথবা নিচে লিখে জানান।",
                        CategoryId: "clarification",
                        CategoryName: "অডিও স্পষ্টীকরণ",
                        QuestionId: null,
                        IsFollowUp: true
                    ),
                    Status: conv.Status,
                    ConversationStatus: conv.Status,
                    CurrentStage: conv.State?.CurrentStage ?? "DetailGathering",
                    CompletionStatus: new ConversationCompletionStatusDto(
                        IsComplete: false,
                        CompletionReason: null,
                        Stage: conv.State?.CurrentStage ?? "DetailGathering",
                        ReadinessPercentage: conv.State?.ReadinessPercentage ?? 0,
                        TotalTurns: conv.State?.TurnCount ?? 0,
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

        // Load or create conversation session
        var conversation = await GetOrCreateConversationAsync(conversationId, incidentType, ct);

        // -------------------------------------------------------------------------
        // STEP 1: Save the user answer immediately
        // -------------------------------------------------------------------------
        var userSequenceNumber = conversation.Messages.Count + 1;
        var userMessage = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "User",
            InputType = inputMessageType,
            Text = userText,
            AudioUrl = audioReference,
            SequenceNumber = userSequenceNumber,
            Timestamp = DateTime.UtcNow
        };
        conversation.Messages.Add(userMessage);

        // Persist user answer immediately to ensure it is never lost during processing
        await _repository.AddMessageAsync(conversation.Id, userMessage, ct);
        _logger.LogInformation("Saved user answer immediately for conversation {ConversationId} (Seq: {Seq}, Type: {Type})",
            conversation.Id, userSequenceNumber, inputMessageType);

        // If the conversation was already completed, acknowledge user input and prevent any new follow-up questions
        if (conversation.IsComplete)
        {
            _logger.LogInformation("Conversation {ConversationId} is already completed. Returning completion acknowledgment without follow-up.", conversation.Id);
            var completedAckMessage = $"আপনার আইনি ফাইলটি ইতিমধ্যে সফলভাবে নথিভুক্ত করা হয়েছে (মামলা নম্বর: {conversation.CaseId ?? "ODH"}). আপনার অতিরিক্ত বার্তাটি নথিতে যুক্ত করা হয়েছে। আমাদের প্যারালিগ্যাল ও লিগ্যাল এইড টিম শীঘ্রই আপনার সাথে যোগাযোগ করবে।";

            var completedSystemMsg = new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = "Assistant",
                InputType = "Text",
                Text = completedAckMessage,
                AudioUrl = null,
                SequenceNumber = conversation.Messages.Count + 1,
                Timestamp = DateTime.UtcNow
            };
            conversation.Messages.Add(completedSystemMsg);
            await _repository.AddMessageAsync(conversation.Id, completedSystemMsg, ct);

            conversation.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateConversationAsync(conversation, ct);

            var existingReport = conversation.FinalReport ?? await _repository.GetReportByConversationIdAsync(conversation.Id, ct);
            GeneratedCaseSummaryDto? existingSummary = null;
            GeneratedApplicationDto? existingApp = null;
            try
            {
                existingSummary = await _caseSummaryGenerator.GetSummaryByConversationIdAsync(conversation.Id, ct);
                existingApp = await _applicationTemplateService.GetGeneratedApplicationByConversationAsync(conversation.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not fetch existing summary or draft for completed conversation");
            }

            return new ChatResponse(
                ConversationId: conversation.Id,
                AssistantMessage: completedAckMessage,
                UserTranscript: inputMessageType == "Voice" ? userText : null,
                InputMessageType: inputMessageType,
                IsFollowUpQuestion: false,
                IsComplete: true,
                IsSafetyEscalated: conversation.IsSafetyEscalated,
                SafetyReason: conversation.SafetyReason,
                Category: conversation.Category,
                ExtractedData: conversation.ExtractedData,
                Timestamp: completedSystemMsg.Timestamp,
                FollowUpQuestion: null,
                Analysis: null,
                State: conversation.State,
                FinalReport: existingReport,
                UserMessage: userText,
                UserMessageResult: new UserMessageResultDto(
                    Text: userText,
                    InputType: inputMessageType,
                    AudioUrl: null,
                    SequenceNumber: userMessage.SequenceNumber,
                    ImportantFactsMentioned: Array.Empty<string>(),
                    NewInformationProvided: Array.Empty<string>(),
                    Timestamp: userMessage.Timestamp
                ),
                AlertInformation: new AlertInformationDto(
                    IsActive: conversation.IsSafetyEscalated,
                    AlertType: conversation.IsSafetyEscalated ? "SafetyEmergency" : "None",
                    Severity: conversation.IsSafetyEscalated ? "Critical" : "Normal",
                    Title: conversation.IsSafetyEscalated ? "জরুরি নিরাপত্তা সতর্কতা" : null,
                    Message: conversation.IsSafetyEscalated ? conversation.SafetyReason : null,
                    MatchedSignal: null,
                    Helpline: conversation.IsSafetyEscalated ? "৯৯৯ (জাতীয় জরুরি সেবা), ১০৯ (নারী ও শিশু নির্যাতন প্রতিরোধ সেল)" : null
                ),
                NextFollowUpQuestion: null,
                NextQuestion: null,
                Status: conversation.Status,
                ConversationStatus: conversation.Status,
                CurrentStage: conversation.State?.CurrentStage ?? "DetailGathering",
                CompletionStatus: new ConversationCompletionStatusDto(
                    IsComplete: true,
                    CompletionReason: conversation.State?.CompletionReason ?? "নথিভুক্তি সম্পন্ন",
                    Stage: conversation.State?.CurrentStage ?? "DetailGathering",
                    ReadinessPercentage: conversation.State?.ReadinessPercentage ?? 100,
                    TotalTurns: conversation.State?.TurnCount ?? conversation.Messages.Count(m => m.Role == "User"),
                    IsAwaitingInput: false
                ),
                CompletionReason: "কথোপকথন ইতিমধ্যে সম্পন্ন হয়েছে",
                CaseId: conversation.CaseId,
                CaseNumber: null,
                GeneratedCaseSummary: existingSummary,
                GeneratedApplication: existingApp
            );
        }

        // -------------------------------------------------------------------------
        // STEP 2: Process the answer (Safety guardrail, NLU, and AI reasoning)
        // -------------------------------------------------------------------------
        var safetyResult = await _safetyGuardrail.ScanAsync(userText, ct);

        if (safetyResult.IsTriggered)
        {
            _logger.LogWarning("Safety guardrail triggered for conversation {ConversationId}: {Reason}",
                conversation.Id, safetyResult.Reason);

            const string emergencyMessage =
                "🚨 জরুরি নিরাপত্তা সতর্কতা — BREAK-OUT: আপনি এখনই বিপদে আছেন বলে মনে হচ্ছে। " +
                "অবিলম্বে সরকারি জাতীয় আইনগত সহায়তা হেল্পলাইন ১৬৪৩০ বা জরুরি সেবা ৯৯৯ এ এখনই কল করুন।";

            // STEP 3 & 4: Generate & Save the emergency system question/advisory
            var emergencySystemMsg = new ConversationMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = "Assistant",
                InputType = "Text",
                Text = emergencyMessage,
                AudioUrl = null,
                SequenceNumber = conversation.Messages.Count + 1,
                Timestamp = DateTime.UtcNow
            };
            conversation.Messages.Add(emergencySystemMsg);
            await _repository.AddMessageAsync(conversation.Id, emergencySystemMsg, ct);

            // Update state and analysis
            conversation.IsSafetyEscalated = true;
            conversation.SafetyReason = safetyResult.Reason;
            conversation.IsComplete = true;
            conversation.Status = "SafetyEscalated";

            conversation.State.ConversationId = conversation.Id;
            conversation.State.Status = "SafetyEscalated";
            conversation.State.CurrentStage = "SafetyEscalated";
            conversation.State.IsComplete = true;
            conversation.State.IsAwaitingInput = false;
            conversation.State.CompletionReason = safetyResult.Reason;
            conversation.State.LastActiveAt = DateTime.UtcNow;

            conversation.Analysis.IsSafetyEscalated = true;
            conversation.Analysis.SafetyReason = safetyResult.Reason;
            conversation.Analysis.AnalyzedAt = DateTime.UtcNow;
            conversation.UpdatedAt = DateTime.UtcNow;

            // Sync URGENT case for paralegal dashboard triage
            var emergencyCase = await SyncCaseAsync(conversation, isUrgent: true, ct);
            await _repository.SaveStateAsync(conversation.Id, conversation.State, ct);
            await _repository.UpdateConversationAsync(conversation, ct);

            // STEP 5: Return emergency alert to client
            var emergencyAlert = new AlertInformationDto(
                IsActive: true,
                AlertType: "SafetyEmergency",
                Severity: "Critical",
                Title: "🚨 জরুরি নিরাপত্তা সতর্কতা — BREAK-OUT",
                Message: "আপনি এখনই বিপদে আছেন বলে মনে হচ্ছে।",
                MatchedSignal: safetyResult.MatchedSignal,
                Helpline: "জাতীয় হেল্পলাইন ১৬৪৩০, জরুরি সেবা ৯৯৯"
            );

            return new ChatResponse(
                ConversationId: conversation.Id,
                AssistantMessage: emergencyMessage,
                UserTranscript: inputMessageType == "Voice" ? userText : null,
                InputMessageType: inputMessageType,
                IsFollowUpQuestion: false,
                IsComplete: true,
                IsSafetyEscalated: true,
                SafetyReason: safetyResult.Reason,
                Category: conversation.Category,
                ExtractedData: conversation.ExtractedData,
                Timestamp: emergencySystemMsg.Timestamp,
                FollowUpQuestion: null,
                Analysis: null,
                State: conversation.State,
                FinalReport: null,
                UserMessage: userText,
                UserMessageResult: new UserMessageResultDto(
                    Text: userText,
                    InputType: inputMessageType,
                    AudioUrl: null,
                    SequenceNumber: userMessage.SequenceNumber,
                    ImportantFactsMentioned: !string.IsNullOrWhiteSpace(safetyResult.Reason) ? new[] { safetyResult.Reason } : Array.Empty<string>(),
                    NewInformationProvided: Array.Empty<string>(),
                    Timestamp: userMessage.Timestamp
                ),
                AlertInformation: emergencyAlert,
                NextFollowUpQuestion: null,
                NextQuestion: null,
                Status: conversation.Status,
                ConversationStatus: conversation.Status,
                CurrentStage: conversation.State?.CurrentStage ?? "SafetyEscalated",
                CompletionStatus: new ConversationCompletionStatusDto(
                    IsComplete: true,
                    CompletionReason: "জরুরি নিরাপত্তা সতর্কতা সক্রিয়",
                    Stage: conversation.State?.CurrentStage ?? "SafetyEscalated",
                    ReadinessPercentage: conversation.State?.ReadinessPercentage ?? 0,
                    TotalTurns: conversation.Messages.Count(m => m.Role == "User"),
                    IsAwaitingInput: false
                ),
                CompletionReason: "জরুরি নিরাপত্তা সতর্কতা সক্রিয়",
                CaseId: emergencyCase?.Id.ToString(),
                CaseNumber: emergencyCase?.CaseNumber
            );
        }

        // Multi-turn context extraction (excluding the current user message)
        var priorHistory = conversation.Messages
            .Where(m => m.Id != userMessage.Id)
            .TakeLast(14)
            .ToList();

        // -------------------------------------------------------------------------
        // OpenRouter LLM Classification & Structured Legal Analysis
        // -------------------------------------------------------------------------
        var openRouterResult = await _openRouter.AnalyzeConversationAsync(
            userText,
            conversation.IncidentType,
            priorHistory,
            conversation.State,
            ct);

        // Map and merge newly extracted facts into conversation state
        var newlyExtracted = MapToExtractedLegalData(openRouterResult.ExtractedInfo);
        conversation.ExtractedData = MergeExtractedData(conversation.ExtractedData, newlyExtracted);

        var categoryCode = openRouterResult.Classification?.CategoryCode ?? conversation.IncidentCategoryCode ?? "OTHER";
        var categoryConfidence = openRouterResult.Classification?.Confidence ?? 0.85;
        var severity = openRouterResult.Severity ?? openRouterResult.Classification?.Severity ?? "Low";
        var severityReason = openRouterResult.SeverityReason ?? openRouterResult.Classification?.SeverityReason;

        // Build strongly typed AIAnalysis document
        var aiAnalysis = new AIAnalysis
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Category = categoryCode,
            CategoryConfidence = categoryConfidence,
            RelevantIncidentTypes = openRouterResult.RelevantIncidentTypes is { Count: > 0 }
                ? openRouterResult.RelevantIncidentTypes
                : [categoryCode],
            Severity = severity,
            SeverityReason = severityReason,
            ImportantFactsMentioned = openRouterResult.ImportantFactsMentioned is { Count: > 0 }
                ? openRouterResult.ImportantFactsMentioned
                : (openRouterResult.ExtractedInfo?.KeyFacts ?? []),
            NewInformationProvided = openRouterResult.NewInformationProvided ?? [],
            MissingFields = openRouterResult.MissingInfo?.MissingFields ?? [],
            MissingDetailsDescription = openRouterResult.MissingInfo?.MissingDetailsDescription,
            IsInformationSufficient = openRouterResult.MissingInfo?.IsInformationSufficient ?? false,
            SuggestedNextQuestionCategory = openRouterResult.SuggestedNextQuestionCategory,
            ConversationComplete = openRouterResult.ConversationComplete,
            IsSafetyEscalated = false,
            SafetyReason = null,
            ExtractedData = conversation.ExtractedData,
            RawAnalysis = openRouterResult,
            AnalyzedAt = DateTime.UtcNow
        };

        // Save the analysis in MongoDB (both embedded in conversation and in ai_analyses collection)
        await _repository.SaveAnalysisAsync(conversation.Id, aiAnalysis, ct);
        _logger.LogInformation("Saved OpenRouter AI analysis for conversation {ConversationId}: Category={Category}, Severity={Severity}",
            conversation.Id, aiAnalysis.Category, aiAnalysis.Severity);

        conversation.Analysis = aiAnalysis;
        if (!string.IsNullOrWhiteSpace(openRouterResult.Classification?.CategoryCode))
        {
            conversation.Category = NormalizeCategory(openRouterResult.Classification.CategoryCode);
        }
        if (conversation.IncidentType == "সাধারণ আইনি সমস্যা" && !string.IsNullOrWhiteSpace(openRouterResult.Classification?.CategoryNameBangla))
        {
            conversation.IncidentType = openRouterResult.Classification.CategoryNameBangla;
        }

        // -------------------------------------------------------------------------
        // Resolve Parent Incident Category & Store Normalized Answers
        // -------------------------------------------------------------------------
        try
        {
            var granularType = openRouterResult.Classification?.CategoryCode;
            if (!string.IsNullOrWhiteSpace(granularType))
            {
                var categories = await _parentCategoryService.GetCategoriesWithIncidentTypesAsync(activeOnly: true, ct);
                var matchedCat = categories.FirstOrDefault(c =>
                    c.IncidentTypes.Any(it => it.Code.Equals(granularType, StringComparison.OrdinalIgnoreCase)));
                if (matchedCat != null)
                {
                    conversation.IncidentCategoryCode = matchedCat.Code;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve parent category for incident type {Type}", openRouterResult.Classification?.CategoryCode);
        }

        // -------------------------------------------------------------------------
        // Extract legal facts from questions & answers and persist to normalized store
        // -------------------------------------------------------------------------
        try
        {
            await _factExtractor.ExtractAndPersistFactsAsync(
                conversation,
                userText,
                inputMessageType,
                openRouterResult,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting legal facts in LegalFactExtractorService for conversation {ConversationId}", conversation.Id);
        }

        // -------------------------------------------------------------------------
        // Update Conversation State tracking from latest analysis
        // -------------------------------------------------------------------------
        var userTurnCount = conversation.Messages.Count(m => m.Role == "User");
        conversation.State.ConversationId = conversation.Id;
        conversation.State.TurnCount = userTurnCount;
        UpdateConversationStateFromAnalysis(conversation.State, conversation.ExtractedData, openRouterResult);

        // -------------------------------------------------------------------------
        // STEP 3: Backend State Validation against AI Completion & Follow-up Execution
        // -------------------------------------------------------------------------
        var validationDecision = _stateValidator.ValidateCompletion(
            openRouterResult.ConversationComplete,
            openRouterResult.MissingInfo,
            conversation.State,
            conversation.IncidentType,
            safetyResult?.IsTriggered == true);

        aiAnalysis.IsBackendValidated = true;
        aiAnalysis.BackendValidationReason = validationDecision.Reason;

        // If backend disallowed completion, override AI claim and update missing info in state
        if (!validationDecision.IsAllowedToComplete && openRouterResult.ConversationComplete)
        {
            _logger.LogWarning(
                "⚠️ Backend validation rejected AI's suggested completion for conversation {ConversationId}. Rationale: {Reason}. Missing: [{Fields}]",
                conversation.Id, validationDecision.Reason, string.Join(", ", validationDecision.MissingMandatoryFields));

            aiAnalysis.ConversationComplete = false;
            foreach (var missingField in validationDecision.MissingMandatoryFields)
            {
                if (!conversation.State.MissingInformation.Contains(missingField, StringComparer.OrdinalIgnoreCase))
                {
                    conversation.State.MissingInformation.Add(missingField);
                }
            }
        }

        var isTurnLimitReached = userTurnCount >= MaxIntakeTurns;
        var isExplicitStop = _stateValidator.IsExplicitStopRequested(userText);

        string systemQuestionText;
        string? followUpQuestionText = null;
        bool isComplete;
        bool isFollowUp;
        string? completionReason = null;
        FollowUpQuestionDecision? followUpDecision = null;

        // -------------------------------------------------------------------------
        // A conversation can end when:
        //   1. The user explicitly wants to stop
        //   2. Required information is collected
        //   3. No important follow-up question remains
        // -------------------------------------------------------------------------
        CaseModel? generatedCase = null;

        // CONDITION 1: The user explicitly wants to stop
        if (isExplicitStop)
        {
            _logger.LogInformation("Conversation {ConversationId} ending: User explicitly requested to stop/conclude.", conversation.Id);
            isComplete = true;
            isFollowUp = false;
            followUpQuestionText = null;
            conversation.IsComplete = true;
            conversation.Status = "Completed";
            completionReason = "ব্যবহারকারী কথোপকথন সমাপ্ত করার অনুরোধ করেছেন";

            generatedCase = await SyncCaseAsync(conversation, isUrgent: false, ct);
            systemQuestionText =
                $"আপনার অনুরোধ অনুযায়ী কথোপকথন সমাপ্ত করা হয়েছে। এ পর্যন্ত সংগৃহীত তথ্যের ভিত্তিতে আপনার প্রাথমিক আইনি ফাইল প্রস্তুত করা হয়েছে (মামলা নম্বর: {generatedCase?.CaseNumber ?? "ODH"}). আমাদের প্যারালিগ্যাল ও লিগ্যাল এইড টিম এটি পর্যালোচনা করে আপনার সাথে যোগাযোগ করবে।";
        }
        // CONDITION 2: Max turn limit reached
        else if (isTurnLimitReached)
        {
            _logger.LogInformation("Conversation {ConversationId} ending: Turn limit ({MaxTurns}) reached.",
                conversation.Id, MaxIntakeTurns);
            isComplete = true;
            isFollowUp = false;
            followUpQuestionText = null;
            conversation.IsComplete = true;
            conversation.Status = "Completed";
            completionReason = "সর্বোচ্চ কথোপকথন সীমা সম্পন্ন হয়েছে";

            generatedCase = await SyncCaseAsync(conversation, isUrgent: false, ct);
            systemQuestionText =
                $"ধন্যবাদ! আপনার ঘটনার প্রয়োজনীয় সকল তথ্য সফলভাবে সংগৃহীত ও সংরক্ষিত হয়েছে (মামলা নম্বর: {generatedCase?.CaseNumber ?? "ODH"}). আপনার অভিযোগের ভিত্তিতে প্রাথমিক আইনি খসড়াটি প্রস্তুত করা হয়েছে। আমাদের প্যারালিগ্যাল টিম এটি দ্রুত পর্যালোচনা করবে।";
        }
        // CONDITION 3: Check if important follow-up questions remain or engine concludes
        else
        {
            // Execute Follow-up Question Engine (reads analysis & state, finds missing facts, prioritizes, selects from Question Bank)
            followUpDecision = await _followUpEngine.DetermineNextQuestionAsync(
                aiAnalysis,
                conversation.State,
                conversation.IncidentType,
                userText,
                safetyResult,
                ct);

            // CONDITION 3: No important follow-up question remains (questions exhausted or facts sufficient)
            if (followUpDecision.IsComplete || string.IsNullOrWhiteSpace(followUpDecision.QuestionText))
            {
                _logger.LogInformation("Conversation {ConversationId} ending: No important follow-up question remains.", conversation.Id);
                isComplete = true;
                isFollowUp = false;
                followUpQuestionText = null;
                conversation.IsComplete = true;
                conversation.Status = "Completed";
                completionReason = "আর কোনো গুরুত্বপূর্ণ ফলো-আপ প্রশ্ন অবশিষ্ট নেই";

                generatedCase = await SyncCaseAsync(conversation, isUrgent: false, ct);
                systemQuestionText =
                    $"ধন্যবাদ! এ পর্যায়ে আপনার সাথে আলোচনার সব প্রয়োজনীয় প্রশ্নের উত্তর সংগৃহীত হয়েছে (মামলা নম্বর: {generatedCase?.CaseNumber ?? "ODH"}). প্রাপ্ত তথ্যের ভিত্তিতে আপনার মামলাটি প্রস্তুত করা হয়েছে এবং প্যারালিগ্যাল টিম এটি পর্যালোচনা করবে।";
            }
            else
            {
                isComplete = false;
                isFollowUp = true;
                followUpQuestionText = followUpDecision.QuestionText;
                systemQuestionText = followUpDecision.QuestionText;

                if (!string.IsNullOrWhiteSpace(followUpDecision.CategoryId) &&
                    !conversation.State.AskedQuestionCategories.Contains(followUpDecision.CategoryId, StringComparer.OrdinalIgnoreCase))
                {
                    conversation.State.AskedQuestionCategories.Add(followUpDecision.CategoryId);
                }

                if (!string.IsNullOrWhiteSpace(followUpDecision.QuestionText))
                {
                    conversation.State.QuestionsAlreadyAsked.Add(followUpDecision.QuestionText);
                }

                _logger.LogInformation("Follow-up Question Engine selected question '{QuestionId}' (Category: '{Category}', Name: '{Name}', Source: {Source})",
                    followUpDecision.QuestionId, followUpDecision.CategoryId, followUpDecision.CategoryName, followUpDecision.SelectionSource);

                // Always sync in-progress case so Paralegal Desk reflects live conversation facts immediately
                generatedCase = await SyncCaseAsync(conversation, isUrgent: false, ct);
            }
        }

        // -------------------------------------------------------------------------
        // STEP 4: Save the system question & persist updated conversation state
        // -------------------------------------------------------------------------
        var systemSequenceNumber = conversation.Messages.Count + 1;
        var systemMessage = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "Assistant",
            InputType = "Text",
            Text = systemQuestionText,
            AudioUrl = null,
            SequenceNumber = systemSequenceNumber,
            Timestamp = DateTime.UtcNow
        };
        conversation.Messages.Add(systemMessage);

        // Persist system message immediately to preserve sequence
        await _repository.AddMessageAsync(conversation.Id, systemMessage, ct);
        _logger.LogInformation("Saved system question for conversation {ConversationId} (Seq: {Seq})",
            conversation.Id, systemSequenceNumber);

        // Update dynamic state and completion tracking
        conversation.IsComplete = isComplete;
        conversation.Status = isComplete ? "Completed" : "Active";
        conversation.State.CurrentStep = isComplete ? userTurnCount : userTurnCount + 1;
        conversation.State.IsComplete = isComplete;
        conversation.State.Status = isComplete ? "Completed" : "Active";
        conversation.State.CurrentStage = DetermineStage(isComplete, conversation.State.ReadinessPercentage, userTurnCount, conversation.ExtractedData);
        conversation.State.IsAwaitingInput = !isComplete;
        conversation.State.NextPrompt = followUpQuestionText;
        conversation.State.CompletionReason = isComplete ? completionReason : null;
        conversation.State.LastActiveAt = DateTime.UtcNow;

        // Persist state in both conversation document and conversation_states collection
        await _repository.SaveStateAsync(conversation.Id, conversation.State, ct);

        conversation.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateConversationAsync(conversation, ct);

        // -------------------------------------------------------------------------
        // Generate Final Conversation Report if conversation has completed:
        // 1. Load all conversation messages (conversation.Messages in chronological sequence)
        // -------------------------------------------------------------------------
        // Generate Case Summary & Final Conversation Report:
        // Case Summary is refreshed on EVERY turn so client and dashboard always have live summary.
        // Final Conversation Report & Legal Draft are generated upon completion.
        // -------------------------------------------------------------------------
        FinalConversationReport? finalReport = null;
        GeneratedCaseSummaryDto? generatedSummary = null;
        GeneratedApplicationDto? generatedApplication = null;

        // Auto-generate / refresh Case Summary on EVERY turn
        try
        {
            generatedSummary = await _caseSummaryGenerator.GenerateSummaryAsync(conversation.Id, ct);
            _logger.LogInformation("Updated live Case Summary {SummaryId} for conversation {ConvId} (Turn: {Turn}, Rate: {Rate}%)",
                generatedSummary.Id, conversation.Id, userTurnCount, generatedSummary.CompletionRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-generate structured Case Summary for conversation {ConvId}", conversation.Id);
        }

        if (isComplete)
        {
            _logger.LogInformation("Conversation {ConversationId} is completed. Generating Final Conversation Report...", conversation.Id);

            try
            {
                // 3. Load saved AI analyses from MongoDB
                var savedAnalyses = await _context.AiAnalyses
                    .Find(a => a.ConversationId == conversation.Id)
                    .ToListAsync(ct);

                if (savedAnalyses.Count == 0 && aiAnalysis != null)
                {
                    savedAnalyses = [aiAnalysis];
                }

                // 4. Generate structured report using OpenRouter LLM
                finalReport = await _openRouter.GenerateFinalReportAsync(
                    conversation,
                    conversation.State,
                    savedAnalyses,
                    ct);

                // 5. Save generated report in MongoDB
                await _repository.SaveReportAsync(finalReport, ct);
                conversation.FinalReport = finalReport;

                _logger.LogInformation("Final Conversation Report {ReportId} generated and saved in MongoDB for conversation {ConversationId}",
                    finalReport.Id, conversation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate or save Final Conversation Report for conversation {ConversationId}", conversation.Id);
            }

            // 7. Select Application Template & Generate Application Draft
            try
            {
                generatedApplication = await _applicationTemplateService.GenerateDraftForConversationAsync(conversation.Id, null, ct);
                _logger.LogInformation("Auto-generated Application Draft {DraftId} for completed conversation {ConvId}",
                    generatedApplication.Id, conversation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-generate legal Application Draft for conversation {ConvId}", conversation.Id);
            }
        }

        _logger.LogInformation("Conversation {ConversationId} Turn Complete. Stage={Stage}, UserTurns={Turns}, Readiness={Readiness}%, IsFollowUp={FollowUp}, Complete={Complete}",
            conversation.Id, conversation.State.CurrentStage, userTurnCount, conversation.State.ReadinessPercentage, isFollowUp, isComplete);

        // -------------------------------------------------------------------------
        // STEP 5: Return the next question, structured analysis, full state, and final report to the client
        // -------------------------------------------------------------------------
        var userFactsMentioned = new List<string>();
        if (openRouterResult.ImportantFactsMentioned is { Count: > 0 })
        {
            userFactsMentioned.AddRange(openRouterResult.ImportantFactsMentioned);
        }
        else if (openRouterResult.ExtractedInfo?.KeyFacts is { Count: > 0 })
        {
            userFactsMentioned.AddRange(openRouterResult.ExtractedInfo.KeyFacts);
        }

        var userNewInfo = new List<string>();
        if (openRouterResult.NewInformationProvided is { Count: > 0 })
        {
            userNewInfo.AddRange(openRouterResult.NewInformationProvided);
        }
        if (!string.IsNullOrWhiteSpace(openRouterResult.ExtractedInfo?.Respondent))
        {
            userNewInfo.Add($"বিবাদী/অভিযুক্ত: {openRouterResult.ExtractedInfo.Respondent}");
        }
        if (!string.IsNullOrWhiteSpace(openRouterResult.ExtractedInfo?.Location))
        {
            userNewInfo.Add($"ঘটনাস্থল: {openRouterResult.ExtractedInfo.Location}");
        }
        if (!string.IsNullOrWhiteSpace(openRouterResult.ExtractedInfo?.IncidentDate))
        {
            userNewInfo.Add($"সময়/তারিখ: {openRouterResult.ExtractedInfo.IncidentDate}");
        }

        var userMessageResult = new UserMessageResultDto(
            Text: userText,
            InputType: inputMessageType,
            AudioUrl: null,
            SequenceNumber: userTurnCount,
            ImportantFactsMentioned: userFactsMentioned,
            NewInformationProvided: userNewInfo,
            Timestamp: userMessage.Timestamp
        );

        var alertInfo = new AlertInformationDto(
            IsActive: safetyResult?.IsTriggered == true,
            AlertType: safetyResult?.IsTriggered == true ? "SafetyEmergency" : "None",
            Severity: safetyResult?.IsTriggered == true ? "Critical" : "Normal",
            Title: safetyResult?.IsTriggered == true ? "জরুরি নিরাপত্তা সতর্কতা" : null,
            Message: safetyResult?.Reason,
            MatchedSignal: safetyResult?.MatchedSignal,
            Helpline: safetyResult?.IsTriggered == true ? "৯৯৯ (জাতীয় জরুরি সেবা), ১০৯ (নারী ও শিশু নির্যাতন প্রতিরোধ সেল)" : null
        );

        NextQuestionDto? nextQuestionDto = null;
        if (!isComplete && !string.IsNullOrWhiteSpace(followUpQuestionText))
        {
            nextQuestionDto = new NextQuestionDto(
                Text: followUpQuestionText,
                CategoryId: followUpDecision?.CategoryId,
                CategoryName: followUpDecision?.CategoryName,
                QuestionId: followUpDecision?.QuestionId,
                IsFollowUp: true
            );
        }

        var completionStatus = new ConversationCompletionStatusDto(
            IsComplete: isComplete,
            CompletionReason: completionReason,
            Stage: conversation.State.CurrentStage,
            ReadinessPercentage: conversation.State.ReadinessPercentage,
            TotalTurns: userTurnCount,
            IsAwaitingInput: !isComplete
        );

        return new ChatResponse(
            ConversationId: conversation.Id,
            AssistantMessage: systemQuestionText,
            UserTranscript: inputMessageType == "Voice" ? userText : null,
            InputMessageType: inputMessageType,
            IsFollowUpQuestion: isFollowUp,
            IsComplete: isComplete,
            IsSafetyEscalated: false,
            SafetyReason: null,
            Category: conversation.Category,
            ExtractedData: conversation.ExtractedData,
            Timestamp: systemMessage.Timestamp,
            FollowUpQuestion: followUpQuestionText,
            Analysis: openRouterResult,
            State: conversation.State,
            FinalReport: finalReport,
            UserMessage: userText,
            UserMessageResult: userMessageResult,
            AlertInformation: alertInfo,
            NextFollowUpQuestion: followUpQuestionText,
            NextQuestion: nextQuestionDto,
            Status: conversation.Status,
            ConversationStatus: conversation.Status,
            CurrentStage: conversation.State.CurrentStage,
            CompletionStatus: completionStatus,
            CompletionReason: completionReason,
            CaseId: generatedCase?.Id.ToString() ?? conversation.CaseId,
            CaseNumber: generatedCase?.CaseNumber,
            GeneratedCaseSummary: generatedSummary,
            GeneratedApplication: generatedApplication
        );
    }

    public async Task<ConversationDetailDto?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await _repository.GetByIdAsync(conversationId, ct);
        return conversation is null ? null : ConversationDetailDto.From(conversation);
    }

    // -----------------------------------------------------------------------------
    // Private Helpers
    // -----------------------------------------------------------------------------

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
                if (!string.IsNullOrWhiteSpace(incidentType))
                {
                    existing.IncidentType = incidentType;
                    var catCode = ResolveParentCategoryCode(incidentType);
                    if (catCode != "OTHER" || string.IsNullOrWhiteSpace(existing.IncidentCategoryCode))
                    {
                        existing.IncidentCategoryCode = catCode;
                        existing.Category = catCode;
                    }
                }
                return existing;
            }
        }

        var parentCatCode = ResolveParentCategoryCode(incidentType);
        var normalizedCat = NormalizeCategory(incidentType ?? "অন্য সমস্যা");
        var newConv = new ConversationModel
        {
            Id = conversationId.HasValue && conversationId.Value != Guid.Empty
                ? conversationId.Value
                : Guid.NewGuid(),
            IncidentType = string.IsNullOrWhiteSpace(incidentType) ? normalizedCat : incidentType.Trim(),
            IncidentCategoryCode = parentCatCode,
            Category = parentCatCode,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        newConv.State.ConversationId = newConv.Id;
        newConv.State.Status = "Active";
        newConv.State.CurrentStep = 1;
        newConv.State.TurnCount = 0;
        newConv.State.IsComplete = false;
        newConv.State.IsAwaitingInput = true;
        newConv.State.LastActiveAt = DateTime.UtcNow;

        newConv.Analysis.ConversationId = newConv.Id;
        newConv.Analysis.Category = parentCatCode;
        newConv.Analysis.AnalyzedAt = DateTime.UtcNow;

        await _repository.CreateAsync(newConv, ct);
        return newConv;
    }

    /// <summary>
    /// Synchronizes a conversation state into an official CaseModel record in MongoDB 'cases' collection,
    /// generating a formal case reference number and rendering the legal complaint text draft via TemplateEngineService.
    /// </summary>
    private async Task<CaseModel> SyncCaseAsync(ConversationModel conversation, bool isUrgent, CancellationToken ct)
    {
        CaseModel? existingCase = null;
        if (!string.IsNullOrWhiteSpace(conversation.CaseId) && Guid.TryParse(conversation.CaseId, out var existingGuid))
        {
            existingCase = await _context.Cases.Find(c => c.Id == existingGuid).FirstOrDefaultAsync(ct);
        }

        var @case = existingCase ?? new CaseModel
        {
            Id = Guid.NewGuid(),
            CaseNumber = await _caseService.GenerateCaseNumberAsync(ct),
            CreatedAt = DateTime.UtcNow
        };

        var parentCode = !string.IsNullOrWhiteSpace(conversation.IncidentCategoryCode)
            ? conversation.IncidentCategoryCode
            : ResolveParentCategoryCode(conversation.IncidentType ?? conversation.Category);

        @case.Category = parentCode;
        @case.CategoryConfidence = 0.95;
        @case.IsSafetyEscalated = isUrgent || conversation.IsSafetyEscalated;
        @case.SafetyReason = conversation.SafetyReason;
        @case.Status = @case.IsSafetyEscalated ? CaseStatuses.Urgent : CaseStatuses.New;
        @case.UpdatedAt = DateTime.UtcNow;

        // 1. Sync all fine-grained legal facts from ConversationState.InformationAlreadyCollected
        if (conversation.State?.InformationAlreadyCollected != null)
        {
            foreach (var (k, v) in conversation.State.InformationAlreadyCollected)
            {
                if (!string.IsNullOrWhiteSpace(v) && !IsGenericPlaceholder(v))
                {
                    @case.ExtractedData[k] = new ExtractedField { Value = v.Trim(), IsInferred = false };
                }
            }
        }

        // 2. Map structured ExtractedData if fields are not already set from InformationAlreadyCollected
        if (conversation.ExtractedData != null)
        {
            var ed = conversation.ExtractedData;
            if (!string.IsNullOrWhiteSpace(ed.Applicant?.Value) && !IsGenericPlaceholder(ed.Applicant.Value) && !@case.ExtractedData.ContainsKey("applicant"))
                @case.ExtractedData["applicant"] = new ExtractedField { Value = ed.Applicant.Value.Trim(), IsInferred = ed.Applicant.IsInferred };

            if (!string.IsNullOrWhiteSpace(ed.Respondent?.Value) && !IsGenericPlaceholder(ed.Respondent.Value) && !@case.ExtractedData.ContainsKey("respondent"))
                @case.ExtractedData["respondent"] = new ExtractedField { Value = ed.Respondent.Value.Trim(), IsInferred = ed.Respondent.IsInferred };

            if (!string.IsNullOrWhiteSpace(ed.MarriageOrIncidentDate?.Value))
            {
                var isFamily = parentCode.Contains("FAMILY", StringComparison.OrdinalIgnoreCase) || parentCode.Contains("DOWER", StringComparison.OrdinalIgnoreCase);
                if (isFamily)
                {
                    if (!@case.ExtractedData.ContainsKey("marriageDate"))
                        @case.ExtractedData["marriageDate"] = new ExtractedField { Value = ed.MarriageOrIncidentDate.Value.Trim(), IsInferred = ed.MarriageOrIncidentDate.IsInferred };
                }
                else
                {
                    if (!@case.ExtractedData.ContainsKey("incidentDate"))
                        @case.ExtractedData["incidentDate"] = new ExtractedField { Value = ed.MarriageOrIncidentDate.Value.Trim(), IsInferred = ed.MarriageOrIncidentDate.IsInferred };
                }
            }

            if (!string.IsNullOrWhiteSpace(ed.DisputedAmount?.Value))
            {
                if (!@case.ExtractedData.ContainsKey("claimedAmount"))
                    @case.ExtractedData["claimedAmount"] = new ExtractedField { Value = ed.DisputedAmount.Value.Trim(), IsInferred = ed.DisputedAmount.IsInferred };

                if (parentCode.Contains("DOWER", StringComparison.OrdinalIgnoreCase) || parentCode.Contains("FAMILY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!@case.ExtractedData.ContainsKey("dowerAmount"))
                        @case.ExtractedData["dowerAmount"] = new ExtractedField { Value = ed.DisputedAmount.Value.Trim(), IsInferred = ed.DisputedAmount.IsInferred };
                }
                else if (parentCode.Contains("LABOUR", StringComparison.OrdinalIgnoreCase) || parentCode.Contains("WAGE", StringComparison.OrdinalIgnoreCase))
                {
                    if (!@case.ExtractedData.ContainsKey("dueWageAmount"))
                        @case.ExtractedData["dueWageAmount"] = new ExtractedField { Value = ed.DisputedAmount.Value.Trim(), IsInferred = ed.DisputedAmount.IsInferred };
                }
                else if (parentCode.Contains("DOWRY", StringComparison.OrdinalIgnoreCase))
                {
                    if (!@case.ExtractedData.ContainsKey("dowryDemandAmount"))
                        @case.ExtractedData["dowryDemandAmount"] = new ExtractedField { Value = ed.DisputedAmount.Value.Trim(), IsInferred = ed.DisputedAmount.IsInferred };
                }
            }

            if (ed.EvidenceList is { Count: > 0 } && !@case.ExtractedData.ContainsKey("evidenceList"))
            {
                @case.ExtractedData["evidenceList"] = new ExtractedField { Value = string.Join(", ", ed.EvidenceList), IsInferred = false };
            }
        }

        // Ensure the client's latest raw voice/text narrative is captured in ExtractedData
        var latestUserMsg = conversation.Messages.LastOrDefault(m => m.Role == "User");
        if (latestUserMsg != null && !string.IsNullOrWhiteSpace(latestUserMsg.Text))
        {
            if (!@case.ExtractedData.ContainsKey("incidentDetails"))
            {
                @case.ExtractedData["incidentDetails"] = new ExtractedField { Value = latestUserMsg.Text, IsInferred = false };
            }
            if (!@case.ExtractedData.ContainsKey("clientStatement"))
            {
                @case.ExtractedData["clientStatement"] = new ExtractedField { Value = latestUserMsg.Text, IsInferred = false };
            }
            if (!@case.ExtractedData.ContainsKey("facts"))
            {
                @case.ExtractedData["facts"] = new ExtractedField { Value = latestUserMsg.Text, IsInferred = false };
            }

            // Quick extraction fallback for applicant and respondent (crucial for immediate safety escalations)
            if (!@case.ExtractedData.ContainsKey("applicant"))
            {
                var matchApp = System.Text.RegularExpressions.Regex.Match(latestUserMsg.Text, @"আমার\s+নাম\s+([^\s।,!?]+(?:\s+[^\s।,!?]+)?)");
                if (matchApp.Success && !IsGenericPlaceholder(matchApp.Groups[1].Value))
                {
                    @case.ExtractedData["applicant"] = new ExtractedField { Value = matchApp.Groups[1].Value.Trim(), IsInferred = false };
                }
            }

            if (!@case.ExtractedData.ContainsKey("respondent"))
            {
                var matchResp = System.Text.RegularExpressions.Regex.Match(latestUserMsg.Text, @"(?:আমার\s+)?স্বামী\s+([^\s।,!?]+(?:\s+[^\s।,!?]+)?)");
                if (matchResp.Success && !IsGenericPlaceholder(matchResp.Groups[1].Value))
                {
                    @case.ExtractedData["respondent"] = new ExtractedField { Value = matchResp.Groups[1].Value.Trim(), IsInferred = false };
                }
            }

            if (!@case.ExtractedData.ContainsKey("dowryDemandAmount") && latestUserMsg.Text.Contains("যৌতুক"))
            {
                var matchAmount = System.Text.RegularExpressions.Regex.Match(latestUserMsg.Text, @"(?:যৌতুক|দাবি)\s*(?:হিসেবে|বাবদ)?\s*([০-৯0-9]+(?:\s*(?:লাখ|লক্ষ|হাজার|টাকা))*[^\s।,!?]*\s*টাকা)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (matchAmount.Success && !string.IsNullOrWhiteSpace(matchAmount.Groups[1].Value))
                {
                    @case.ExtractedData["dowryDemandAmount"] = new ExtractedField { Value = matchAmount.Groups[1].Value.Trim(), IsInferred = false };
                }
            }
        }

        var location = conversation.Analysis?.RawAnalysis?.ExtractedInfo?.Location;
        if (!string.IsNullOrWhiteSpace(location))
        {
            @case.ExtractedData["district"] = new ExtractedField { Value = location, IsInferred = false };
            @case.ExtractedData["location"] = new ExtractedField { Value = location, IsInferred = false };
        }
        else if (!@case.ExtractedData.ContainsKey("district"))
        {
            @case.ExtractedData["district"] = new ExtractedField { Value = "ঢাকা", IsInferred = true };
        }

        if (!string.IsNullOrWhiteSpace(conversation.IncidentType))
        {
            @case.ExtractedData["caseType"] = new ExtractedField { Value = conversation.IncidentType, IsInferred = false };
        }
        else
        {
            @case.ExtractedData["caseType"] = new ExtractedField { Value = @case.Category, IsInferred = false };
        }

        if (@case.IsSafetyEscalated)
        {
            @case.ExtractedData["safetyAlert"] = new ExtractedField { Value = @case.SafetyReason ?? "শারীরিক নির্যাতন ঝুঁকি", IsInferred = false };
        }

        // Map dialogue history
        @case.DialogueHistory = conversation.Messages
            .Where(m => m.Role == "User")
            .Select((m, idx) =>
            {
                var correspondingAssistant = conversation.Messages
                    .FirstOrDefault(a => a.Role == "Assistant" && a.Timestamp >= m.Timestamp);

                return new DialogueTurn
                {
                    TurnIndex = idx,
                    Transcript = m.Text,
                    AudioUrl = m.AudioUrl,
                    SystemPrompt = correspondingAssistant?.Text ?? string.Empty,
                    SafetyTriggered = @case.IsSafetyEscalated
                };
            })
            .ToList();

        // Deterministically render pre-approved Bangla complaint text
        @case.DraftComplaintText = _templateEngine.RenderComplaint(@case);

        await _context.Cases.ReplaceOneAsync(
            c => c.Id == @case.Id,
            @case,
            new ReplaceOptions { IsUpsert = true },
            ct);

        conversation.CaseId = @case.Id.ToString();
        _logger.LogInformation("Synced Conversation {ConversationId} to Case {CaseNumber} (Status: {Status}, Category: {Category})",
            conversation.Id, @case.CaseNumber, @case.Status, @case.Category);

        return @case;
    }

    private static ExtractedLegalData MapToExtractedLegalData(ExtractedInformation? info)
    {
        if (info == null) return new ExtractedLegalData();

        return new ExtractedLegalData(
            Applicant: !string.IsNullOrWhiteSpace(info.Applicant) ? new InferredField(info.Applicant, false) : null,
            Respondent: !string.IsNullOrWhiteSpace(info.Respondent) ? new InferredField(info.Respondent, false) : null,
            MarriageOrIncidentDate: !string.IsNullOrWhiteSpace(info.IncidentDate) ? new InferredField(info.IncidentDate, false) : null,
            DisputedAmount: !string.IsNullOrWhiteSpace(info.DisputedAmount) ? new InferredField(info.DisputedAmount, false) : null,
            EvidenceList: info.EvidenceList
        );
    }

    private static ExtractedLegalData MergeExtractedData(ExtractedLegalData? existing, ExtractedLegalData? incoming)
    {
        if (existing == null) return incoming ?? new ExtractedLegalData();
        if (incoming == null) return existing;

        var applicant = !string.IsNullOrWhiteSpace(incoming.Applicant?.Value) ? incoming.Applicant : existing.Applicant;
        var respondent = !string.IsNullOrWhiteSpace(incoming.Respondent?.Value) ? incoming.Respondent : existing.Respondent;
        var date = !string.IsNullOrWhiteSpace(incoming.MarriageOrIncidentDate?.Value) ? incoming.MarriageOrIncidentDate : existing.MarriageOrIncidentDate;
        var amount = !string.IsNullOrWhiteSpace(incoming.DisputedAmount?.Value) ? incoming.DisputedAmount : existing.DisputedAmount;

        var evidence = new HashSet<string>(existing.EvidenceList ?? []);
        if (incoming.EvidenceList != null)
        {
            foreach (var ev in incoming.EvidenceList)
            {
                if (!string.IsNullOrWhiteSpace(ev)) evidence.Add(ev);
            }
        }

        return new ExtractedLegalData(
            Applicant: applicant,
            Respondent: respondent,
            MarriageOrIncidentDate: date,
            DisputedAmount: amount,
            EvidenceList: evidence.ToList()
        );
    }

    public static string ResolveParentCategoryCode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "OTHER";
        var trimmed = input.Trim();
        var upper = trimmed.ToUpperInvariant();

        if (upper is "DOWER_MAINTENANCE" or "DOWRY_DOMESTIC_VIOLENCE" or "LABOUR_WAGE" or "LAND_PROPERTY" or "WOMEN_CHILD_PROTECTION" or "OTHER")
            return upper;

        if (trimmed.Contains("দেনমোহর") || trimmed.Contains("ভরণপোষণ") || upper.Contains("DOWER") || upper.Contains("MAINTENANCE"))
            return "DOWER_MAINTENANCE";
        if (trimmed.Contains("যৌতুক") || trimmed.Contains("সহিংসতা") || trimmed.Contains("নির্যাতন") || upper.Contains("DOWRY") || upper.Contains("DOMESTIC"))
            return "DOWRY_DOMESTIC_VIOLENCE";
        if (trimmed.Contains("মজুরি") || trimmed.Contains("শ্রম") || trimmed.Contains("বেতন") || upper.Contains("WAGE") || upper.Contains("LABOUR") || upper.Contains("LABOR"))
            return "LABOUR_WAGE";
        if (trimmed.Contains("জমি") || trimmed.Contains("সম্পত্তি") || upper.Contains("LAND") || upper.Contains("PROPERTY"))
            return "LAND_PROPERTY";
        if (trimmed.Contains("নারী") || trimmed.Contains("শিশু") || upper.Contains("WOMEN") || upper.Contains("CHILD"))
            return "WOMEN_CHILD_PROTECTION";

        return "OTHER";
    }

    private static string NormalizeCategory(string category)
    {
        var code = ResolveParentCategoryCode(category);
        return code switch
        {
            "DOWER_MAINTENANCE" => "দেনমোহর / ভরণপোষণ",
            "DOWRY_DOMESTIC_VIOLENCE" => "যৌতুক / পারিবারিক সহিংসতা",
            "LABOUR_WAGE" => "বকেয়া মজুরি / শ্রম",
            "LAND_PROPERTY" => "জমি / সম্পত্তি বিরোধ",
            "WOMEN_CHILD_PROTECTION" => "নারী ও শিশু সুরক্ষা",
            _ => "অন্য সমস্যা"
        };
    }

    private static bool HasRequiredFacts(string category, ExtractedLegalData? data)
    {
        if (data == null) return false;
        var hasParties = !string.IsNullOrWhiteSpace(data.Applicant?.Value) || !string.IsNullOrWhiteSpace(data.Respondent?.Value);
        var hasDetails = !string.IsNullOrWhiteSpace(data.MarriageOrIncidentDate?.Value) || !string.IsNullOrWhiteSpace(data.DisputedAmount?.Value);
        return hasParties && hasDetails;
    }

    private static void UpdateConversationStateFromAnalysis(
        ConversationState state,
        ExtractedLegalData? extractedData,
        OpenRouterAnalysisResult analysis)
    {
        // 1. Detected incident types across conversation
        if (!string.IsNullOrWhiteSpace(analysis.Classification?.CategoryCode) &&
            !state.DetectedIncidentTypes.Contains(analysis.Classification.CategoryCode, StringComparer.OrdinalIgnoreCase))
        {
            state.DetectedIncidentTypes.Add(analysis.Classification.CategoryCode);
        }

        if (analysis.RelevantIncidentTypes != null)
        {
            foreach (var type in analysis.RelevantIncidentTypes)
            {
                if (!string.IsNullOrWhiteSpace(type) && !state.DetectedIncidentTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                {
                    state.DetectedIncidentTypes.Add(type);
                }
            }
        }

        // 2. Information already collected
        if (!string.IsNullOrWhiteSpace(extractedData?.Applicant?.Value))
        {
            state.InformationAlreadyCollected["applicant"] = extractedData.Applicant.Value;
        }
        else if (!string.IsNullOrWhiteSpace(analysis.ExtractedInfo?.Applicant))
        {
            state.InformationAlreadyCollected["applicant"] = analysis.ExtractedInfo.Applicant;
        }

        if (!string.IsNullOrWhiteSpace(extractedData?.Respondent?.Value))
        {
            state.InformationAlreadyCollected["respondent"] = extractedData.Respondent.Value;
        }
        else if (!string.IsNullOrWhiteSpace(analysis.ExtractedInfo?.Respondent))
        {
            state.InformationAlreadyCollected["respondent"] = analysis.ExtractedInfo.Respondent;
        }

        if (!string.IsNullOrWhiteSpace(extractedData?.MarriageOrIncidentDate?.Value))
        {
            state.InformationAlreadyCollected["incident_date"] = extractedData.MarriageOrIncidentDate.Value;
        }
        else if (!string.IsNullOrWhiteSpace(analysis.ExtractedInfo?.IncidentDate))
        {
            state.InformationAlreadyCollected["incident_date"] = analysis.ExtractedInfo.IncidentDate;
        }

        if (!string.IsNullOrWhiteSpace(extractedData?.DisputedAmount?.Value))
        {
            state.InformationAlreadyCollected["disputed_amount"] = extractedData.DisputedAmount.Value;
        }
        else if (!string.IsNullOrWhiteSpace(analysis.ExtractedInfo?.DisputedAmount))
        {
            state.InformationAlreadyCollected["disputed_amount"] = analysis.ExtractedInfo.DisputedAmount;
        }

        if (extractedData?.EvidenceList is { Count: > 0 })
        {
            state.InformationAlreadyCollected["evidence"] = string.Join("; ", extractedData.EvidenceList);
        }
        else if (analysis.ExtractedInfo?.EvidenceList is { Count: > 0 })
        {
            state.InformationAlreadyCollected["evidence"] = string.Join("; ", analysis.ExtractedInfo.EvidenceList);
        }

        if (analysis.ExtractedInfo?.KeyFacts is { Count: > 0 })
        {
            state.InformationAlreadyCollected["key_facts"] = string.Join("; ", analysis.ExtractedInfo.KeyFacts);
        }

        if (analysis.NewInformationProvided is { Count: > 0 })
        {
            state.InformationAlreadyCollected["latest_user_update"] = string.Join("; ", analysis.NewInformationProvided);
        }

        // 3. Missing information
        state.MissingInformation = analysis.MissingInfo?.MissingFields ?? [];
        state.MissingDetailsDescription = analysis.MissingInfo?.MissingDetailsDescription;

        // 4. Readiness percentage
        state.ReadinessPercentage = CalculateReadinessPercentage(extractedData, analysis.MissingInfo ?? new MissingInformation());
    }

    private static int CalculateReadinessPercentage(ExtractedLegalData? data, MissingInformation missingInfo)
    {
        if (missingInfo.IsInformationSufficient) return 100;

        var score = 20; // Base score for initiating legal intake
        if (!string.IsNullOrWhiteSpace(data?.Respondent?.Value)) score += 25;
        if (!string.IsNullOrWhiteSpace(data?.MarriageOrIncidentDate?.Value)) score += 20;
        if (!string.IsNullOrWhiteSpace(data?.DisputedAmount?.Value)) score += 20;
        if (data?.EvidenceList is { Count: > 0 }) score += 15;

        return Math.Clamp(score, 0, 95);
    }

    private static string DetermineStage(bool isComplete, int readiness, int turnCount, ExtractedLegalData? data)
    {
        if (isComplete) return "Completed";
        if (readiness >= 80 || (data?.EvidenceList is { Count: > 0 })) return "Review";
        if (!string.IsNullOrWhiteSpace(data?.Respondent?.Value) &&
            (!string.IsNullOrWhiteSpace(data?.MarriageOrIncidentDate?.Value) || !string.IsNullOrWhiteSpace(data?.DisputedAmount?.Value)))
        {
            return "EvidenceCollection";
        }
        if (turnCount > 0) return "FactGathering";
        return "Classification";
    }

    private static bool IsDuplicateOrAlreadyCollected(string? question, ConversationState state)
    {
        if (string.IsNullOrWhiteSpace(question)) return true;

        var norm = NormalizeQuestion(question);

        // 1. Direct or fuzzy match with questions already asked
        foreach (var asked in state.QuestionsAlreadyAsked)
        {
            var askedNorm = NormalizeQuestion(asked);
            if (askedNorm == norm || (norm.Length > 12 && (askedNorm.Contains(norm) || norm.Contains(askedNorm))))
            {
                return true;
            }
        }

        // 2. Fact already collected checks (prevent redundant inquiries)
        if (state.InformationAlreadyCollected.ContainsKey("respondent") &&
            (norm.Contains("অভিযুক্ত") || norm.Contains("স্বামী") || norm.Contains("বিবাদী") || (norm.Contains("নাম") && norm.Contains("পরিচয়"))))
        {
            return true;
        }

        if (state.InformationAlreadyCollected.ContainsKey("incident_date") &&
            (norm.Contains("বিয়ে") || norm.Contains("বিয়ে") || norm.Contains("তারিখ") || norm.Contains("কবে") || norm.Contains("ঘটেছিল")))
        {
            return true;
        }

        if (state.InformationAlreadyCollected.ContainsKey("disputed_amount") &&
            (norm.Contains("দেনমোহর") || norm.Contains("টাকা") || norm.Contains("পরিমাণ") || norm.Contains("বকেয়া") || norm.Contains("ক্ষতি")))
        {
            return true;
        }

        if (state.InformationAlreadyCollected.ContainsKey("evidence") &&
            (norm.Contains("প্রমাণ") || norm.Contains("কাবিননামা") || norm.Contains("কাগজ") || norm.Contains("দলিল") || norm.Contains("রশিদ")))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeQuestion(string text)
    {
        return text.Trim()
            .Replace("?", "")
            .Replace("।", "")
            .Replace("!", "")
            .Replace(",", "")
            .Replace(" ", "");
    }

    private static bool IsGenericPlaceholder(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        var placeholders = new[] { "ভুক্তভোগী", "ভুক্তভোগী নাগরিক", "নাগরিক", "বাদী", "আবেদনকারী", "অভিযোগকারী", "বিবাদী", "অভিযুক্ত", "প্রতিপক্ষ", "মালিক", "অজ্ঞাত", "অজ্ঞাতনামা", "তথ্য পাওয়া যায়নি" };
        return placeholders.Any(p => trimmed.Equals(p, StringComparison.OrdinalIgnoreCase));
    }
}
