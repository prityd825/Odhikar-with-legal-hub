using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

/// <summary>
/// Conversational AI Controller providing multi-turn interactive dialogue with
/// Google Gemini using voice or text input.
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class ChatController(
    IConversationOrchestrator orchestrator,
    ILogger<ChatController> logger) : ControllerBase
{
    private readonly IConversationOrchestrator _orchestrator = orchestrator;
    private readonly ILogger<ChatController> _logger = logger;

    /// <summary>
    /// Processes a conversational turn. Accepts multipart/form-data with either text, voice audio, or both.
    /// Uses ONLY Google Gemini API for speech transcription and conversational reasoning.
    /// </summary>
    [HttpPost("message")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB cap
    public async Task<ActionResult<ChatResponse>> SendMessage(
        [FromForm] ChatRequest request,
        CancellationToken ct)
    {
        try
        {
            var incidentCategoryOrType = !string.IsNullOrWhiteSpace(request.CategoryCode)
                ? request.CategoryCode
                : request.IncidentType;

            var response = await _orchestrator.ProcessTurnAsync(
                request.ConversationId,
                incidentCategoryOrType,
                request.Text,
                request.Audio,
                ct);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing chat turn for conversation {ConversationId}",
                request.ConversationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "কথোপকথন প্রক্রিয়াকরণে অপ্রত্যাশিত সমস্যা হয়েছে। অনুগ্রহ করে আবার চেষ্টা করুন।" });
        }
    }

    /// <summary>
    /// Convenient JSON endpoint for text-only turns.
    /// </summary>
    [HttpPost("text")]
    public async Task<ActionResult<ChatResponse>> SendTextMessage(
        [FromBody] ChatTextRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "টেক্সট খালি রাখা যাবে না।" });
        }

        try
        {
            var incidentCategoryOrType = !string.IsNullOrWhiteSpace(request.CategoryCode)
                ? request.CategoryCode
                : request.IncidentType;

            var response = await _orchestrator.ProcessTurnAsync(
                request.ConversationId,
                incidentCategoryOrType,
                request.Text,
                null,
                ct);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing text chat turn for conversation {ConversationId}",
                request.ConversationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "কথোপকথন প্রক্রিয়াকরণে অপ্রত্যাশিত সমস্যা হয়েছে। অনুগ্রহ করে আবার চেষ্টা করুন।" });
        }
    }

    /// <summary>
    /// Retrieves a conversation and its complete chronological turn history.
    /// </summary>
    [HttpGet("{conversationId:guid}")]
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        CancellationToken ct)
    {
        var conversation = await _orchestrator.GetConversationAsync(conversationId, ct);
        if (conversation is null)
        {
            return NotFound(new { message = "কথোপকথন সেশন পাওয়া যায়নি।" });
        }

        return Ok(conversation);
    }

    /// <summary>
    /// Analyzes conversation text using the OpenRouter LLM service, returning structured JSON with
    /// incident classification, extracted facts, missing fields, suggested question category, and completion status.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<OpenRouterAnalysisResult>> Analyze(
        [FromBody] OpenRouterAnalysisRequest request,
        [FromServices] IOpenRouterLlmService openRouter,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.ConversationText))
        {
            return BadRequest(new { message = "কথোপকথনের টেক্সট প্রয়োজন।" });
        }

        var result = await openRouter.AnalyzeTurnAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the generated Final Conversation Report for a completed intake conversation.
    /// </summary>
    [HttpGet("{conversationId:guid}/report")]
    [HttpGet("conversations/{conversationId:guid}/report")]
    public async Task<ActionResult<FinalConversationReport>> GetFinalReport(
        Guid conversationId,
        [FromServices] IConversationRepository repository,
        CancellationToken ct)
    {
        var report = await repository.GetReportByConversationIdAsync(conversationId, ct);
        if (report is null)
        {
            return NotFound(new { message = "এই কথোপকথনের জন্য চূড়ান্ত প্রতিবেদন এখনও প্রস্তুত হয়নি বা পাওয়া যায়নি।" });
        }

        return Ok(report);
    }

    /// <summary>
    /// Synthesises native Bangla speech (MP3) for playing client transcripts or assistant dialogues.
    /// </summary>
    [HttpGet("tts")]
    [HttpPost("tts")]
    [HttpGet("/api/intake/tts")]
    [HttpPost("/api/intake/tts")]
    public async Task<IActionResult> TextToSpeech(
        [FromQuery] string? q,
        [FromBody] TtsRequest? request,
        [FromServices] ITextToSpeechService tts,
        CancellationToken ct)
    {
        var text = !string.IsNullOrWhiteSpace(q) ? q : request?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { message = "টেক্সট খালি রাখা যাবে না।" });
        }

        var audio = await tts.SynthesizeAsync(text, ct);
        if (audio.Length == 0)
        {
            return NoContent();
        }

        return File(audio, "audio/mpeg");
    }
}
