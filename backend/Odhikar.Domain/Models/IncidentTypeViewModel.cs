namespace Odhikar.Domain.Models;

/// <summary>
/// View model / DTO representation of incident types for API clients.
/// </summary>
public sealed class IncidentTypeViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ParentCategoryCode { get; set; }
    public bool IsActive { get; set; }

    public static IncidentTypeViewModel From(IncidentTypeModel model) => new()
    {
        Id = model.Id.ToString(),
        Code = model.Code,
        Name = model.Name,
        NameEn = model.NameEn,
        Category = model.Category,
        ParentCategoryCode = model.ParentCategoryCode,
        IsActive = model.IsActive
    };
}
