using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

public sealed class ParentCategoryService(MongoDbContext context, ILogger<ParentCategoryService> logger) : IParentCategoryService
{
    private readonly MongoDbContext _context = context;
    private readonly ILogger<ParentCategoryService> _logger = logger;

    public async Task<IReadOnlyList<ParentCategoryDto>> GetAllAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var filter = activeOnly
            ? Builders<ParentIncidentCategoryModel>.Filter.Eq(x => x.IsActive, true)
            : Builders<ParentIncidentCategoryModel>.Filter.Empty;

        var items = await _context.ParentIncidentCategories
            .Find(filter)
            .SortBy(x => x.Order)
            .ToListAsync(ct);

        return items.Select(ParentCategoryDto.From).ToList();
    }

    public async Task<ParentCategoryDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var item = await _context.ParentIncidentCategories
            .Find(x => x.Code == code.ToUpperInvariant())
            .FirstOrDefaultAsync(ct);

        return item is null ? null : ParentCategoryDto.From(item);
    }

    public async Task<IReadOnlyList<ParentCategoryWithIncidentsDto>> GetCategoriesWithIncidentTypesAsync(
        bool activeOnly = true, CancellationToken ct = default)
    {
        var catFilter = activeOnly
            ? Builders<ParentIncidentCategoryModel>.Filter.Eq(x => x.IsActive, true)
            : Builders<ParentIncidentCategoryModel>.Filter.Empty;

        var categories = await _context.ParentIncidentCategories
            .Find(catFilter)
            .SortBy(x => x.Order)
            .ToListAsync(ct);

        var incFilter = activeOnly
            ? Builders<IncidentTypeModel>.Filter.Eq(x => x.IsActive, true)
            : Builders<IncidentTypeModel>.Filter.Empty;

        var incidents = await _context.IncidentTypes
            .Find(incFilter)
            .ToListAsync(ct);

        // Group incidents by parentCategoryCode (case-insensitive)
        var incidentsGrouped = incidents
            .Where(i => !string.IsNullOrWhiteSpace(i.ParentCategoryCode))
            .GroupBy(i => i.ParentCategoryCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new List<ParentCategoryWithIncidentsDto>();
        foreach (var cat in categories)
        {
            var childIncidents = incidentsGrouped.TryGetValue(cat.Code, out var list)
                ? list
                : [];

            result.Add(ParentCategoryWithIncidentsDto.From(cat, childIncidents));
        }

        return result;
    }

    public async Task<IReadOnlyList<IncidentTypeViewModel>> GetIncidentTypesByParentCategoryAsync(
        string parentCategoryCode, bool activeOnly = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parentCategoryCode)) return [];

        var filterBuilder = Builders<IncidentTypeModel>.Filter;
        var filter = filterBuilder.Eq(x => x.ParentCategoryCode, parentCategoryCode.ToUpperInvariant());
        if (activeOnly)
        {
            filter &= filterBuilder.Eq(x => x.IsActive, true);
        }

        var items = await _context.IncidentTypes.Find(filter).ToListAsync(ct);
        return items.Select(IncidentTypeViewModel.From).ToList();
    }

    public async Task<IReadOnlyList<IncidentTypeViewModel>> GetIncidentTypesAsync(
        bool activeOnly = true, string? parentCategoryCode = null, CancellationToken ct = default)
    {
        var builder = Builders<IncidentTypeModel>.Filter;
        var filter = activeOnly
            ? builder.Eq(x => x.IsActive, true)
            : builder.Empty;

        if (!string.IsNullOrWhiteSpace(parentCategoryCode))
        {
            filter &= builder.Eq(x => x.ParentCategoryCode, parentCategoryCode.Trim().ToUpperInvariant());
        }

        var items = await _context.IncidentTypes.Find(filter).ToListAsync(ct);
        return items.Select(IncidentTypeViewModel.From).ToList();
    }

    public async Task<IncidentTypeViewModel?> GetIncidentTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _context.IncidentTypes.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        return item is null ? null : IncidentTypeViewModel.From(item);
    }
}
