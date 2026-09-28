namespace Odhikar.Domain.Models;

/// <summary>
/// Defines an extensible question category used during conversational legal intake.
/// Categories can be configured via appsettings.json or loaded from built-in defaults.
/// </summary>
public sealed class QuestionCategoryDefinition
{
    /// <summary>
    /// Unique category identifier (e.g. "current_safety", "threat_details", "injury_details",
    /// "perpetrator", "incident_description", "incident_time", "incident_location", "financial_details",
    /// "evidence_documents", "witness_details", "desired_relief").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable category display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Brief description of why and when this category is needed.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Evaluation priority order (lower numbers are addressed first, e.g. Safety=10, Threat=20, Injury=30, Perpetrator=40).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// The primary fact key in ConversationState.InformationAlreadyCollected mapped to this category.
    /// If this fact key is already present, the category may be considered satisfied.
    /// </summary>
    public string? RequiredFactKey { get; set; }

    /// <summary>
    /// Pre-approved, legally vetted Bengali question prompt.
    /// Used as fallback or deterministic system question.
    /// </summary>
    public string DefaultQuestionBangla { get; set; } = string.Empty;

    /// <summary>
    /// Optional list of incident types this category applies to (empty list indicates applicable to all types).
    /// </summary>
    public List<string> ApplicableIncidentTypes { get; set; } = [];

    /// <summary>
    /// Indicates whether collecting information for this category is mandatory before completing intake.
    /// </summary>
    public bool IsMandatory { get; set; } = false;

    /// <summary>
    /// Group categorization e.g. "Safety", "CoreFacts", "Context", "Evidence", "Relief".
    /// </summary>
    public string Group { get; set; } = "CoreFacts";

    /// <summary>
    /// True if this category is currently active.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
