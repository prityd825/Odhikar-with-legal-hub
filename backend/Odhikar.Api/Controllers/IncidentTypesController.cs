using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Dtos;

namespace Odhikar.Api.Controllers;

/// <summary>
/// Controller for fetching and managing legal incident types.
/// </summary>
[ApiController]
[Route("api/incident-types")]
public sealed class IncidentTypesController(IParentCategoryService parentCategoryService) : ControllerBase
{
    private readonly IParentCategoryService _parentCategoryService = parentCategoryService;

    /// <summary>Returns legal incident types, optionally filtered by active status and parent category code.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentTypeViewModel>>> GetIncidentTypes(
        [FromQuery] bool activeOnly = true,
        [FromQuery] string? parentCategoryCode = null,
        CancellationToken ct = default)
    {
        var items = await _parentCategoryService.GetIncidentTypesAsync(activeOnly, parentCategoryCode, ct);
        return Ok(items);
    }

    /// <summary>Returns a specific incident type by ID.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<IncidentTypeViewModel>> GetIncidentTypeById(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var guidId))
        {
            return NotFound(new { message = "Incident type not found." });
        }

        var item = await _parentCategoryService.GetIncidentTypeByIdAsync(guidId, ct);
        if (item is null)
        {
            return NotFound(new { message = "Incident type not found." });
        }

        return Ok(item);
    }
}
