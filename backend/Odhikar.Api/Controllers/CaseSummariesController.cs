using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

[ApiController]
[Route("api/case-summaries")]
public sealed class CaseSummariesController : ControllerBase
{
    private readonly ICaseSummaryGeneratorService _generatorService;
    private readonly ILogger<CaseSummariesController> _logger;

    public CaseSummariesController(
        ICaseSummaryGeneratorService generatorService,
        ILogger<CaseSummariesController> logger)
    {
        _generatorService = generatorService;
        _logger = logger;
    }

    /// <summary>
    /// Generates and persists a structured case summary for a conversation using its resolved category template.
    /// Unavailable values are marked as 'তথ্য পাওয়া যায়নি', and initial status is 'DRAFT'.
    /// </summary>
    [HttpPost("generate/{conversationId:guid}")]
    [ProducesResponseType(typeof(GeneratedCaseSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateSummary(Guid conversationId, CancellationToken ct)
    {
        try
        {
            var summary = await _generatorService.GenerateSummaryAsync(conversationId, ct);
            return Ok(summary);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves the persisted case summary for a conversation.
    /// </summary>
    [HttpGet("conversation/{conversationId:guid}")]
    [ProducesResponseType(typeof(GeneratedCaseSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummaryByConversation(Guid conversationId, CancellationToken ct)
    {
        var summary = await _generatorService.GetSummaryByConversationIdAsync(conversationId, ct);
        if (summary == null)
        {
            return NotFound(new { message = $"No generated case summary found for conversation '{conversationId}'." });
        }
        return Ok(summary);
    }

    /// <summary>
    /// Retrieves a generated case summary by its primary ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GeneratedCaseSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummaryById(Guid id, CancellationToken ct)
    {
        var summary = await _generatorService.GetSummaryByIdAsync(id, ct);
        if (summary == null)
        {
            return NotFound(new { message = $"Case summary with ID '{id}' not found." });
        }
        return Ok(summary);
    }

    /// <summary>
    /// Updates the workflow status of a generated summary (e.g. 'DRAFT' -> 'APPROVED').
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(GeneratedCaseSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSummaryStatusRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Status cannot be empty." });
        }

        var updated = await _generatorService.UpdateSummaryStatusAsync(id, request.Status, ct);
        if (updated == null)
        {
            return NotFound(new { message = $"Case summary with ID '{id}' not found." });
        }
        return Ok(updated);
    }
}
