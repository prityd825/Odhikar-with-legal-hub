using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

public interface IParentCategoryService
{
    Task<IReadOnlyList<ParentCategoryDto>> GetAllAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<ParentCategoryDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<ParentCategoryWithIncidentsDto>> GetCategoriesWithIncidentTypesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<IReadOnlyList<IncidentTypeViewModel>> GetIncidentTypesByParentCategoryAsync(string parentCategoryCode, bool activeOnly = true, CancellationToken ct = default);
    Task<IReadOnlyList<IncidentTypeViewModel>> GetIncidentTypesAsync(bool activeOnly = true, string? parentCategoryCode = null, CancellationToken ct = default);
    Task<IncidentTypeViewModel?> GetIncidentTypeByIdAsync(Guid id, CancellationToken ct = default);
}
