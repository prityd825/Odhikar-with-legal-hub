using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/application-templates")]
[Route("api/application-drafts")]
[Produces("application/json")]
public sealed class ApplicationTemplatesController : ControllerBase
{
    private readonly IApplicationTemplateService _service;
    private readonly ILogger<ApplicationTemplatesController> _logger;

    public ApplicationTemplatesController(
        IApplicationTemplateService service,
        ILogger<ApplicationTemplatesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Lists all active application templates for legal drafting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApplicationTemplateDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var templates = await _service.GetTemplatesAsync(activeOnly, ct);
        return Ok(templates);
    }

    /// <summary>
    /// Retrieves a specific application template by category code.
    /// </summary>
    [HttpGet("{categoryCode}")]
    [ProducesResponseType<ApplicationTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplateByCategory(
        string categoryCode,
        CancellationToken ct = default)
    {
        var template = await _service.GetTemplateByCategoryAsync(categoryCode, ct);
        if (template == null)
        {
            return NotFound(new { error = $"Application template for category '{categoryCode}' was not found." });
        }

        return Ok(template);
    }

    /// <summary>
    /// Generates a formal legal application draft (DRAFT / PENDING_REVIEW) based on conversation and case summary data.
    /// Pipeline: Conversation -> Incident Category -> Generated Case Summary -> Application Template -> Placeholder Resolution -> Saved Draft.
    /// </summary>
    [HttpPost("generate/{conversationId:guid}")]
    [ProducesResponseType<GeneratedApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateDraft(
        Guid conversationId,
        [FromBody] GenerateApplicationRequest? request = null,
        CancellationToken ct = default)
    {
        try
        {
            var generated = await _service.GenerateDraftForConversationAsync(conversationId, request, ct);
            return Ok(generated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate application draft for conversation {ConvId}", conversationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Application draft generation failed." });
        }
    }

    /// <summary>
    /// Retrieves the generated legal application draft for a conversation.
    /// </summary>
    [HttpGet("conversation/{conversationId:guid}")]
    [ProducesResponseType<GeneratedApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDraftByConversation(
        Guid conversationId,
        CancellationToken ct = default)
    {
        var draft = await _service.GetGeneratedApplicationByConversationAsync(conversationId, ct);
        if (draft == null)
        {
            return NotFound(new { error = $"No generated application draft found for conversation '{conversationId}'." });
        }

        return Ok(draft);
    }

    /// <summary>
    /// Retrieves a generated application by its primary ID.
    /// </summary>
    [HttpGet("generated/{id:guid}")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GeneratedApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGeneratedById(
        Guid id,
        CancellationToken ct = default)
    {
        var draft = await _service.GetGeneratedApplicationByIdAsync(id, ct);
        if (draft == null)
        {
            return NotFound(new { error = $"Generated application '{id}' was not found." });
        }

        return Ok(draft);
    }

    /// <summary>
    /// Exports the generated legal application draft for a conversation to a downloadable PDF.
    /// </summary>
    [HttpGet("conversation/{conversationId:guid}/export-pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> ExportPdfByConversation(
        Guid conversationId,
        [FromServices] IPdfService pdfService,
        CancellationToken ct = default)
    {
        var draft = await _service.GetGeneratedApplicationByConversationAsync(conversationId, ct);
        if (draft == null)
        {
            // Try generating it on the fly
            draft = await _service.GenerateDraftForConversationAsync(conversationId, null, ct);
        }

        var pdfModel = new ApplicationPdfDocumentModel
        {
            CaseNumber = draft.CaseId?.ToString() ?? conversationId.ToString()[..8].ToUpperInvariant(),
            ParentCategoryCode = draft.IncidentCategoryCode,
            ParentCategoryNameBn = draft.TemplateTitleBn ?? string.Empty,
            TemplateTitleBn = draft.TemplateTitleBn ?? string.Empty,
            RecipientAuthorityBn = draft.RecipientAuthorityBn ?? string.Empty,
            SubjectBn = draft.Subject ?? string.Empty,
            ApplicationBodyBn = draft.ApplicationText ?? string.Empty,
            DisclaimerNoticeBn = draft.DisclaimerNotice ?? string.Empty,
            Status = draft.Status ?? "DRAFT",
            ReviewStatus = draft.ReviewStatus ?? "PENDING_REVIEW",
            CreatedAt = draft.CreatedAt,
            PlaceholdersUsed = draft.PlaceholdersUsed ?? new Dictionary<string, string>()
        };

        var bytes = pdfService.GenerateApplicationPdf(pdfModel);
        return File(bytes, "application/pdf", $"{draft.IncidentCategoryCode}_{conversationId}.pdf");
    }

    /// <summary>
    /// Exports a specific generated application draft by its primary ID to a downloadable PDF.
    /// </summary>
    [HttpGet("generated/{id:guid}/export-pdf")]
    [HttpGet("{id:guid}/export-pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> ExportPdfByGeneratedId(
        Guid id,
        [FromServices] IPdfService pdfService,
        CancellationToken ct = default)
    {
        var draft = await _service.GetGeneratedApplicationByIdAsync(id, ct);
        if (draft == null)
        {
            return NotFound(new { error = $"Generated application '{id}' was not found." });
        }

        var pdfModel = new ApplicationPdfDocumentModel
        {
            CaseNumber = draft.CaseId?.ToString() ?? id.ToString()[..8].ToUpperInvariant(),
            ParentCategoryCode = draft.IncidentCategoryCode,
            ParentCategoryNameBn = draft.TemplateTitleBn ?? string.Empty,
            TemplateTitleBn = draft.TemplateTitleBn ?? string.Empty,
            RecipientAuthorityBn = draft.RecipientAuthorityBn ?? string.Empty,
            SubjectBn = draft.Subject ?? string.Empty,
            ApplicationBodyBn = draft.ApplicationText ?? string.Empty,
            DisclaimerNoticeBn = draft.DisclaimerNotice ?? string.Empty,
            Status = draft.Status ?? "DRAFT",
            ReviewStatus = draft.ReviewStatus ?? "PENDING_REVIEW",
            CreatedAt = draft.CreatedAt,
            PlaceholdersUsed = draft.PlaceholdersUsed ?? new Dictionary<string, string>()
        };

        var bytes = pdfService.GenerateApplicationPdf(pdfModel);
        return File(bytes, "application/pdf", $"{draft.IncidentCategoryCode}_{id}.pdf");
    }

    /// <summary>
    /// Exports the generated application template PDF for a specific case ID.
    /// </summary>
    [HttpGet("cases/{caseId:guid}/export-pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> ExportPdfByCaseId(
        Guid caseId,
        [FromServices] ICaseService caseService,
        [FromServices] IPdfService pdfService,
        CancellationToken ct = default)
    {
        var @case = await caseService.GetByIdAsync(caseId.ToString(), ct);
        if (@case == null)
        {
            return NotFound(new { error = $"Case '{caseId}' was not found." });
        }

        var pdfModel = await _service.BuildApplicationPdfModelForCaseAsync(@case, ct);
        var bytes = pdfService.GenerateApplicationPdf(pdfModel);
        var safeCaseNum = string.IsNullOrWhiteSpace(@case.CaseNumber) ? caseId.ToString() : @case.CaseNumber;

        return File(bytes, "application/pdf", $"{pdfModel.ParentCategoryCode}_{safeCaseNum}.pdf");
    }

    /// <summary>
    /// Updates the review/approval status of a generated legal application draft.
    /// </summary>
    [HttpPatch("generated/{id:guid}/status")]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<GeneratedApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReviewStatus(
        Guid id,
        [FromBody] UpdateApplicationReviewStatusRequest request,
        CancellationToken ct = default)
    {
        var updated = await _service.UpdateReviewStatusAsync(id, request, ct);
        if (updated == null)
        {
            return NotFound(new { error = $"Generated application '{id}' was not found." });
        }

        return Ok(updated);
    }
}
