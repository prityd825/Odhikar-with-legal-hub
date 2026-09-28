namespace Odhikar.Application.Dtos;

/// <summary>
/// Result returned by the Follow-up Question Engine encapsulating the selected question,
/// prioritized missing legal fact, source, reasoning, and intake completion status.
/// </summary>
public sealed record FollowUpQuestionDecision(
    bool IsComplete,
    string? QuestionText,
    string? QuestionId,
    string? CategoryId,
    string? CategoryName,
    string? PrioritizedMissingField,
    string SelectionSource,
    string Reasoning,
    IReadOnlyList<string> MissingInformationFound
);
