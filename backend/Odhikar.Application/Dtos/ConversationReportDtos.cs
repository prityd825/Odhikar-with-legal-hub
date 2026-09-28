using System.Text.Json.Serialization;

namespace Odhikar.Application.Dtos;

/// <summary>
/// Strongly typed JSON mapping for OpenRouter LLM final conversation report output.
/// </summary>
public sealed record OpenRouterFinalReportJson(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("primaryCategory")] string? PrimaryCategory,
    [property: JsonPropertyName("detectedIncidentTypes")] List<string>? DetectedIncidentTypes,
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("severityReason")] string? SeverityReason,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("importantEvents")] List<string>? ImportantEvents,
    [property: JsonPropertyName("chronologicalTimeline")] List<OpenRouterTimelineItem>? ChronologicalTimeline,
    [property: JsonPropertyName("safetyConcerns")] OpenRouterSafetyReportItem? SafetyConcerns,
    [property: JsonPropertyName("missingInformation")] List<string>? MissingInformation,
    [property: JsonPropertyName("extractedFacts")] Dictionary<string, string>? ExtractedFacts,
    [property: JsonPropertyName("legalAssessment")] string? LegalAssessment,
    [property: JsonPropertyName("recommendedNextSteps")] List<string>? RecommendedNextSteps
);

public sealed record OpenRouterTimelineItem(
    [property: JsonPropertyName("dateOrTime")] string? DateOrTime,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("sourceTurn")] int? SourceTurn
);

public sealed record OpenRouterSafetyReportItem(
    [property: JsonPropertyName("hasSafetyConcerns")] bool HasSafetyConcerns,
    [property: JsonPropertyName("isEmergencyEscalated")] bool IsEmergencyEscalated,
    [property: JsonPropertyName("safetySummary")] string? SafetySummary,
    [property: JsonPropertyName("riskFactors")] List<string>? RiskFactors
);
