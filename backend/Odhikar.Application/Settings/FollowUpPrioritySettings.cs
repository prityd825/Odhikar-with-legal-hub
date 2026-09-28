namespace Odhikar.Application.Settings;

/// <summary>
/// Configurable priority rules for the Follow-up Question Engine.
/// Defines the priority order of question categories and whether alarming detection
/// signals should elevate safety-related categories to the highest priority.
/// </summary>
public sealed class FollowUpPrioritySettings
{
    /// <summary>
    /// When true, any alarming safety signal (detected by SafetyGuardrailService or high AI severity)
    /// elevates immediate safety and threat questions to the top priority.
    /// </summary>
    public bool ElevateOnAlarmingDetection { get; set; } = true;

    /// <summary>
    /// Configurable list of priority tiers, evaluated in ascending order of Priority.
    /// </summary>
    public List<PriorityTierRule> PriorityTiers { get; set; } = GetDefaultTiers();

    /// <summary>
    /// Resolves the priority score for a given category based on configured tiers.
    /// Lower score = higher priority.
    /// </summary>
    public int GetCategoryPriority(string categoryId, bool isAlarmingSignal)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return 999;

        var normalized = categoryId.Trim().ToLowerInvariant();

        // If alarming signal is detected, elevate immediate safety and current threat to top priority
        if (ElevateOnAlarmingDetection && isAlarmingSignal)
        {
            if (normalized == "current_safety") return 1;
            if (normalized == "threat_details" || normalized == "injury_details") return 2;
        }

        // Find tier matching this category
        foreach (var tier in PriorityTiers.OrderBy(t => t.Priority))
        {
            if (tier.Categories.Any(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return tier.Priority * 10;
            }
        }

        return 500; // Default fallback for unconfigured categories
    }

    /// <summary>
    /// Returns the standard default 7 priority tiers requested.
    /// </summary>
    public static List<PriorityTierRule> GetDefaultTiers() =>
    [
        new()
        {
            TierName = "Immediate safety",
            Priority = 1,
            Categories = ["current_safety"]
        },
        new()
        {
            TierName = "Current threat",
            Priority = 2,
            Categories = ["threat_details", "injury_details"]
        },
        new()
        {
            TierName = "Incident type clarification",
            Priority = 3,
            Categories = ["perpetrator", "incident_type_clarification"]
        },
        new()
        {
            TierName = "Incident description",
            Priority = 4,
            Categories = ["incident_description"]
        },
        new()
        {
            TierName = "Time",
            Priority = 5,
            Categories = ["incident_time"]
        },
        new()
        {
            TierName = "Location",
            Priority = 6,
            Categories = ["incident_location"]
        },
        new()
        {
            TierName = "Other supporting details",
            Priority = 7,
            Categories = ["financial_details", "evidence_documents", "witness_details", "desired_relief"]
        }
    ];
}

/// <summary>
/// A rule defining a priority tier, its integer rank, and the category codes associated with it.
/// </summary>
public sealed class PriorityTierRule
{
    public string TierName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public List<string> Categories { get; set; } = [];
}
