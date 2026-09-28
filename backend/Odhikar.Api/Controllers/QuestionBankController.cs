using Microsoft.AspNetCore.Mvc;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

/// <summary>
/// REST controller for browsing, inspecting, and managing the MongoDB Question Bank.
/// </summary>
[ApiController]
[Route("api/question-bank")]
public sealed class QuestionBankController(
    IQuestionBankService questionBankService,
    IQuestionCategoryService categoryService) : ControllerBase
{
    private readonly IQuestionBankService _questionBank = questionBankService;
    private readonly IQuestionCategoryService _categoryService = categoryService;

    /// <summary>
    /// Retrieves all active questions in the question bank, with optional category, incident type, and parent category filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuestionBankItem>>> GetQuestions(
        [FromQuery] string? category,
        [FromQuery] string? incidentType,
        [FromQuery] string? parentCategory,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(parentCategory))
        {
            var byParent = await _questionBank.GetByParentCategoryAsync(parentCategory, ct);
            return Ok(byParent);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var filtered = await _questionBank.GetByCategoryAsync(category, incidentType, ct);
            return Ok(filtered);
        }

        var all = await _questionBank.GetAllAsync(ct);
        if (!string.IsNullOrWhiteSpace(incidentType))
        {
            var incUpper = incidentType.ToUpperInvariant();
            all = all.Where(q =>
                q.ParentCategoryCodes.Contains("ALL", StringComparer.OrdinalIgnoreCase) ||
                q.ParentCategoryCodes.Contains(incUpper, StringComparer.OrdinalIgnoreCase) ||
                q.RelatedIncidentTypes.Count == 0 ||
                q.RelatedIncidentTypes.Contains("ALL", StringComparer.OrdinalIgnoreCase) ||
                q.RelatedIncidentTypes.Any(t => incUpper.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(incUpper, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        return Ok(all);
    }

    /// <summary>
    /// Retrieves all active questions mapped to a parent incident category (e.g. "DOWER_MAINTENANCE", "DOWRY_DOMESTIC_VIOLENCE").
    /// </summary>
    [HttpGet("by-parent/{parentCategoryCode}")]
    public async Task<ActionResult<IReadOnlyList<QuestionBankItem>>> GetByParentCategory(
        string parentCategoryCode,
        CancellationToken ct)
    {
        var items = await _questionBank.GetByParentCategoryAsync(parentCategoryCode, ct);
        return Ok(items);
    }

    /// <summary>
    /// Retrieves a specific question by its QuestionId (e.g. "Q-DOWER-001").
    /// </summary>
    [HttpGet("{questionId}")]
    public async Task<ActionResult<QuestionBankItem>> GetByQuestionId(
        string questionId,
        CancellationToken ct)
    {
        var item = await _questionBank.GetByQuestionIdAsync(questionId, ct);
        if (item is null)
        {
            return NotFound(new { message = $"Question '{questionId}' not found." });
        }
        return Ok(item);
    }

    /// <summary>
    /// Adds a new question to the MongoDB Question Bank.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<QuestionBankItem>> AddQuestion(
        [FromBody] QuestionBankItem request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.QuestionBangla))
        {
            return BadRequest(new { message = "Category and QuestionBangla are required fields." });
        }

        var created = await _questionBank.AddQuestionAsync(request, ct);
        return CreatedAtAction(nameof(GetByQuestionId), new { questionId = created.QuestionId }, created);
    }

    /// <summary>
    /// Lists all question categories recognized by the system.
    /// </summary>
    [HttpGet("categories")]
    public ActionResult<IReadOnlyList<QuestionCategoryDefinition>> GetCategories(
        [FromQuery] string? incidentType)
    {
        var categories = _categoryService.GetApplicableCategories(incidentType);
        return Ok(categories);
    }
}
