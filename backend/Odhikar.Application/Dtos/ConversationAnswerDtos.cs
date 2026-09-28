using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

/// <summary>
/// Public DTO representing a single normalized question answer record.
/// </summary>
public sealed record ConversationAnswerDto(
    Guid Id,
    Guid? ConversationId,
    Guid? SessionId,
    Guid? CaseId,
    string QuestionCode,
    string QuestionText,
    string Answer,
    int? SelectedOptionId,
    IReadOnlyList<int> SelectedOptionIds,
    IReadOnlyList<string> SelectedOptionTexts,
    DateTime AnsweredAt,
    string Source,
    double? Confidence,
    IReadOnlyDictionary<string, string> Metadata
)
{
    public static ConversationAnswerDto From(ConversationAnswerModel model) =>
        new(
            model.Id,
            model.ConversationId,
            model.SessionId,
            model.CaseId,
            model.QuestionCode,
            model.QuestionText,
            model.Answer,
            model.SelectedOptionId,
            model.SelectedOptionIds,
            model.SelectedOptionTexts,
            model.AnsweredAt,
            model.Source,
            model.Confidence,
            model.Metadata
        );
}

/// <summary>
/// Comprehensive response wrapping both the normalized answer objects and
/// the key-value dictionary keyed by stable question codes.
/// </summary>
public sealed record AnswersDictionaryResponse(
    int TotalAnswers,
    IReadOnlyDictionary<string, string> Dictionary,
    IReadOnlyList<ConversationAnswerDto> Answers
);

/// <summary>
/// Direct request body to record an answer against a conversation, session, or case.
/// </summary>
public sealed class SaveAnswerRequest
{
    public Guid? ConversationId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? CaseId { get; set; }
    public required string QuestionCode { get; set; }
    public string? QuestionText { get; set; }
    public required string Answer { get; set; }
    public int? SelectedOptionId { get; set; }
    public List<int>? SelectedOptionIds { get; set; }
    public List<string>? SelectedOptionTexts { get; set; }
    public string? Source { get; set; }
    public double? Confidence { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
