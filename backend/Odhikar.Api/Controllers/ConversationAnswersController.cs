using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

[ApiController]
[Route("api/conversation-answers")]
public sealed class ConversationAnswersController : ControllerBase
{
    private readonly IConversationAnswerService _answerService;
    private readonly ILogger<ConversationAnswersController> _logger;

    public ConversationAnswersController(
        IConversationAnswerService answerService,
        ILogger<ConversationAnswersController> logger)
    {
        _answerService = answerService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all normalized answers and the generated question-code dictionary for a follow-up session.
    /// </summary>
    [HttpGet("session/{sessionId:guid}")]
    [ProducesResponseType(typeof(AnswersDictionaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnswersBySession(Guid sessionId, CancellationToken ct)
    {
        var answers = await _answerService.GetAnswersBySessionIdAsync(sessionId, ct);
        var dict = await _answerService.GetAnswerDictionaryAsync(null, sessionId, null, ct);

        return Ok(new AnswersDictionaryResponse(
            TotalAnswers: answers.Count,
            Dictionary: dict,
            Answers: answers
        ));
    }

    /// <summary>
    /// Retrieves all normalized answers and the question-code dictionary for an AI conversation.
    /// </summary>
    [HttpGet("conversation/{conversationId:guid}")]
    [ProducesResponseType(typeof(AnswersDictionaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnswersByConversation(Guid conversationId, CancellationToken ct)
    {
        var answers = await _answerService.GetAnswersByConversationIdAsync(conversationId, ct);
        var dict = await _answerService.GetAnswerDictionaryAsync(conversationId, null, null, ct);

        return Ok(new AnswersDictionaryResponse(
            TotalAnswers: answers.Count,
            Dictionary: dict,
            Answers: answers
        ));
    }

    /// <summary>
    /// Retrieves all normalized answers and the question-code dictionary for a legal case.
    /// </summary>
    [HttpGet("case/{caseId:guid}")]
    [ProducesResponseType(typeof(AnswersDictionaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnswersByCase(Guid caseId, CancellationToken ct)
    {
        var answers = await _answerService.GetAnswersByCaseIdAsync(caseId, ct);
        var dict = await _answerService.GetAnswerDictionaryAsync(null, null, caseId, ct);

        return Ok(new AnswersDictionaryResponse(
            TotalAnswers: answers.Count,
            Dictionary: dict,
            Answers: answers
        ));
    }

    /// <summary>
    /// Manually records a normalized answer for a given question code.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConversationAnswerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveAnswer([FromBody] SaveAnswerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionCode))
        {
            return BadRequest(new { message = "QuestionCode আবশ্যক।" });
        }

        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            return BadRequest(new { message = "Answer আবশ্যক।" });
        }

        var model = new ConversationAnswerModel
        {
            ConversationId = request.ConversationId,
            SessionId = request.SessionId,
            CaseId = request.CaseId,
            QuestionCode = request.QuestionCode,
            QuestionText = request.QuestionText ?? string.Empty,
            Answer = request.Answer,
            SelectedOptionId = request.SelectedOptionId,
            SelectedOptionIds = request.SelectedOptionIds ?? [],
            SelectedOptionTexts = request.SelectedOptionTexts ?? [],
            AnsweredAt = DateTime.UtcNow,
            Source = request.Source ?? "ManualEntry",
            Confidence = request.Confidence ?? 1.0,
            Metadata = request.Metadata ?? []
        };

        await _answerService.SaveAnswerAsync(model, ct);
        return CreatedAtAction(nameof(GetAnswersBySession), new { sessionId = model.SessionId ?? Guid.Empty }, ConversationAnswerDto.From(model));
    }
}
