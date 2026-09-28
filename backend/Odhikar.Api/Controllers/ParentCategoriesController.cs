using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

[ApiController]
[Route("api/parent-categories")]
public class ParentCategoriesController : ControllerBase
{
    private readonly IParentCategoryService _parentCategoryService;
    private readonly ILogger<ParentCategoriesController> _logger;

    public ParentCategoriesController(
        IParentCategoryService parentCategoryService,
        ILogger<ParentCategoriesController> logger)
    {
        _parentCategoryService = parentCategoryService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all active parent incident categories.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ParentCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _parentCategoryService.GetAllAsync();
        return Ok(categories);
    }

    /// <summary>
    /// Retrieves all parent incident categories with their associated granular incident types.
    /// </summary>
    [HttpGet("with-incident-types")]
    [ProducesResponseType(typeof(IEnumerable<ParentCategoryWithIncidentsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWithIncidentTypes()
    {
        var result = await _parentCategoryService.GetCategoriesWithIncidentTypesAsync();
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single parent incident category by its unique code.
    /// </summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ParentCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var category = await _parentCategoryService.GetByCodeAsync(code);
        if (category == null)
        {
            return NotFound(new { message = $"Parent category '{code}' not found." });
        }
        return Ok(category);
    }

    /// <summary>
    /// Retrieves granular incident types belonging to a specific parent category code.
    /// </summary>
    [HttpGet("{code}/incident-types")]
    [ProducesResponseType(typeof(IEnumerable<IncidentTypeViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidentTypesByParent(string code)
    {
        var types = await _parentCategoryService.GetIncidentTypesByParentCategoryAsync(code);
        return Ok(types);
    }
}
