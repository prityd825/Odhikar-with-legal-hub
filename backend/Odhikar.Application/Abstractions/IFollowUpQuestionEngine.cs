using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>
/// Follow-up Question Engine that determines the next intake question based on
/// AI analysis, conversation state, missing facts, and the MongoDB Question Bank.
/// </summary>
public interface IFollowUpQuestionEngine
{
    /// <summary>
    /// Executes the 6-step follow-up question flow:
    /// 1. Reads latest AI analysis
    /// 2. Reads conversation state
    /// 3. Finds missing information
    /// 4. Checks questions already asked
    /// 5. Prioritizes the most important missing information
    /// 6. Selects a suitable question from the Question Bank
    /// </summary>
    Task<FollowUpQuestionDecision> DetermineNextQuestionAsync(
        AIAnalysis? latestAnalysis,
        ConversationState state,
        string? incidentType,
        string? userTranscript = null,
        SafetyResult? safetyScanResult = null,
        CancellationToken ct = default);
}
