using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Result of validating AI's completion assessment against ConversationState.
/// </summary>
public sealed record StateValidationDecision(
    bool IsAllowedToComplete,
    bool OverriddenByBackend,
    string Reason,
    IReadOnlyList<string> MissingMandatoryFields
);

/// <summary>
/// Validates AI completion and information sufficiency assertions against
/// verified facts in ConversationState before allowing the conversation to conclude.
/// </summary>
public interface IConversationStateValidator
{
    /// <summary>
    /// Validates whether the conversation is truly complete based on verified facts
    /// stored in ConversationState.InformationAlreadyCollected.
    /// </summary>
    StateValidationDecision ValidateCompletion(
        bool aiSuggestedComplete,
        MissingInformation? aiMissingInfo,
        ConversationState state,
        string? incidentType,
        bool isAlarmingSignal = false);

    /// <summary>
    /// Checks whether the user explicitly conveyed their wish to stop, cancel,
    /// or conclude the conversation (in Bangla or English).
    /// </summary>
    bool IsExplicitStopRequested(string? text);
}
