using System.Collections.Frozen;

namespace Odhikar.Domain.Models;

/// <summary>
/// Legal categories handled by Odhikar. Kept as string constants (not an enum)
/// so they persist cleanly in MongoDB and stay stable across API contracts.
/// </summary>
public static class LegalCategories
{
    // Canonical Parent Incident Categories
    public const string DowerMaintenance = "DOWER_MAINTENANCE";
    public const string DowryDomesticViolence = "DOWRY_DOMESTIC_VIOLENCE";
    public const string LabourWage = "LABOUR_WAGE";
    public const string LandProperty = "LAND_PROPERTY";
    public const string WomenChildProtection = "WOMEN_CHILD_PROTECTION";
    public const string Other = "OTHER";

    // Legacy aliases
    public const string Dowry = "DOWRY";
    public const string UnpaidWages = "UNPAID_WAGES";
    public const string LandDispute = "LAND_DISPUTE";
    public const string OutOfScope = "OUT_OF_SCOPE";
    public const string Unknown = "UNKNOWN";

    public static readonly FrozenSet<string> InScope =
        new[] {
            DowerMaintenance,
            DowryDomesticViolence,
            LabourWage,
            LandProperty,
            WomenChildProtection,
            Other,
            Dowry,
            UnpaidWages,
            LandDispute
        }.ToFrozenSet();

    public static readonly FrozenSet<string> All =
        new[] {
            DowerMaintenance,
            DowryDomesticViolence,
            LabourWage,
            LandProperty,
            WomenChildProtection,
            Other,
            Dowry,
            UnpaidWages,
            LandDispute,
            OutOfScope,
            Unknown
        }.ToFrozenSet();

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Case workflow status values, ordered loosely by triage urgency.</summary>
public static class CaseStatuses
{
    public const string Urgent = "URGENT";
    public const string New = "NEW";
    public const string Assigned = "ASSIGNED";
    public const string Resolved = "RESOLVED";
}
