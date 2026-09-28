using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

[ApiController]
[Route("api/case-summary-templates")]
public sealed class CaseSummaryTemplatesController : ControllerBase
{
    private readonly ICaseSummaryTemplateService _templateService;
    private readonly ILogger<CaseSummaryTemplatesController> _logger;

    public CaseSummaryTemplatesController(
        ICaseSummaryTemplateService templateService,
        ILogger<CaseSummaryTemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active case summary templates.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseSummaryTemplateDto>>> GetTemplates(CancellationToken ct)
    {
        var templates = await _templateService.GetTemplatesAsync(true, ct);
        return Ok(templates);
    }

    /// <summary>
    /// Gets the template for a parent legal category code.
    /// </summary>
    [HttpGet("category/{categoryCode}")]
    public async Task<ActionResult<CaseSummaryTemplateDto>> GetTemplateByCategory(string categoryCode, CancellationToken ct)
    {
        var template = await _templateService.GetTemplateByParentCategoryAsync(categoryCode, ct);
        if (template == null)
        {
            return NotFound(new { message = $"Template for category '{categoryCode}' not found." });
        }
        return Ok(template);
    }
}