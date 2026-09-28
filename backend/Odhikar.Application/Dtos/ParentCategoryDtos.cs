using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

public sealed record ParentCategoryDto(
    string Id,
    string Code,
    string Name,
    string? NameEn,
    string? Description,
    string? Icon,
    int Order,
    bool IsActive
)
{
    public static ParentCategoryDto From(ParentIncidentCategoryModel m) => new(
        Id: m.Id.ToString(),
        Code: m.Code,
        Name: m.Name,
        NameEn: m.NameEn,
        Description: m.Description,
        Icon: m.Icon,
        Order: m.Order,
        IsActive: m.IsActive
    );
}

public sealed record ParentCategoryWithIncidentsDto(
    string Id,
    string Code,
    string Name,
    string? NameEn,
    string? Description,
    string? Icon,
    int Order,
    bool IsActive,
    IReadOnlyList<IncidentTypeViewModel> IncidentTypes
)
{
    public static ParentCategoryWithIncidentsDto From(
        ParentIncidentCategoryModel m,
        IEnumerable<IncidentTypeModel> incidents) => new(
        Id: m.Id.ToString(),
        Code: m.Code,
        Name: m.Name,
        NameEn: m.NameEn,
        Description: m.Description,
        Icon: m.Icon,
        Order: m.Order,
        IsActive: m.IsActive,
        IncidentTypes: incidents.Select(IncidentTypeViewModel.From).ToList()
    );
}
