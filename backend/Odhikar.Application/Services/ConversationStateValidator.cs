using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Application.Services;

/// <summary>
/// Enforces strict backend validation on AI completion claims.
/// Cross-examines AI's suggested completion against verified facts in ConversationState.InformationAlreadyCollected.
/// Prevents premature conversation termination when critical legal facts or safety assurances are missing.
/// </summary>
public sealed class ConversationStateValidator(ILogger<ConversationStateValidator> logger) : IConversationStateValidator
{
    private readonly ILogger<ConversationStateValidator> _logger = logger;
    private const int MaxTurnsCap = 25;

    public StateValidationDecision ValidateCompletion(
        bool aiSuggestedComplete,
        MissingInformation? aiMissingInfo,
        ConversationState state,
        string? incidentType,
        bool isAlarmingSignal = false)
    {
        var collected = state.InformationAlreadyCollected;
        var missingMandatory = new List<string>();

        _logger.LogInformation("Validating AI completion status (AI suggested: {AiComplete}) for conversation {ConversationId} (Turn: {Turn})",
            aiSuggestedComplete, state.ConversationId, state.TurnCount);

        // Turn Cap Rule: If citizen reached maximum turns, allow completion to avoid trapping the user
        if (state.TurnCount >= MaxTurnsCap)
        {
            _logger.LogInformation("Turn quota ({MaxTurns}) reached for conversation {ConversationId}. Allowing completion.",
                MaxTurnsCap, state.ConversationId);

            return new StateValidationDecision(
                IsAllowedToComplete: true,
                OverriddenByBackend: !aiSuggestedComplete,
                Reason: "সর্বোচ্চ কথোপকথন সীমা সম্পন্ন হয়েছে (Turn limit reached).",
                MissingMandatoryFields: []
            );
        }

        // ---------------------------------------------------------------------
        // Check 1: Mandatory Applicant & Opposing Party (Respondent)
        // ---------------------------------------------------------------------
        if (!collected.TryGetValue("applicant", out var applicant) || string.IsNullOrWhiteSpace(applicant) || IsGenericPlaceholderName(applicant))
        {
            missingMandatory.Add("applicant");
        }

        if (!collected.TryGetValue("respondent", out var respondent) || string.IsNullOrWhiteSpace(respondent) || IsGenericPlaceholderName(respondent))
        {
            if (!collected.TryGetValue("employerName", out var emp) || string.IsNullOrWhiteSpace(emp) || IsGenericPlaceholderName(emp))
            {
                missingMandatory.Add("respondent");
            }
        }

        // ---------------------------------------------------------------------
        // Check 2: Incident-Specific Mandatory Prerequisites
        // ---------------------------------------------------------------------
        var incUpper = (incidentType ?? string.Empty).ToUpperInvariant();

        if (incUpper.Contains("DOWER") || incUpper.Contains("MAINTENANCE") || incUpper.Contains("দেনমোহর") || incUpper.Contains("ভরণপোষণ"))
        {
            // Requires either disputed_amount (dower/monthly demand) OR incident_date (marriage/eviction timeline)
            var hasAmount = collected.TryGetValue("disputed_amount", out var amount) && !string.IsNullOrWhiteSpace(amount);
            var hasDate = collected.TryGetValue("incident_date", out var date) && !string.IsNullOrWhiteSpace(date);

            if (!hasAmount && !hasDate)
            {
                missingMandatory.Add("disputed_amount_or_incident_date");
            }
        }
        else if (incUpper.Contains("DOWRY") || incUpper.Contains("যৌতুক"))
        {
            // Requires specific demand details
            var hasAmount = collected.TryGetValue("disputed_amount", out var amount) && !string.IsNullOrWhiteSpace(amount);
            if (!hasAmount)
            {
                missingMandatory.Add("disputed_amount");
            }
        }
        else if (incUpper.Contains("WAGE") || incUpper.Contains("বেতন"))
        {
            var hasAmount = collected.TryGetValue("disputed_amount", out var amount) && !string.IsNullOrWhiteSpace(amount);
            if (!hasAmount)
            {
                missingMandatory.Add("disputed_amount");
            }
        }
        else if (incUpper.Contains("LAND") || incUpper.Contains("জমি"))
        {
            var hasLoc = collected.TryGetValue("incident_location", out var loc) && !string.IsNullOrWhiteSpace(loc);
            if (!hasLoc)
            {
                missingMandatory.Add("incident_location");
            }
        }

        // ---------------------------------------------------------------------
        // Check 3: Safety & Threat Assurance
        // If alarming distress/danger was flagged, safety must be addressed
        // ---------------------------------------------------------------------
        if (isAlarmingSignal)
        {
            var hasSafety = collected.TryGetValue("current_safety", out var safety) && !string.IsNullOrWhiteSpace(safety);
            var hasThreat = collected.TryGetValue("threat_details", out var threat) && !string.IsNullOrWhiteSpace(threat);

            if (!hasSafety && !hasThreat)
            {
                missingMandatory.Add("current_safety");
            }
        }

        // ---------------------------------------------------------------------
        // Decision Synthesis
        // ---------------------------------------------------------------------
        if (missingMandatory.Count > 0)
        {
            // Backend REJECTS completion because mandatory facts are absent from state
            var reason = $"বাকি প্রয়োজনীয় আইনি তথ্য আবশ্যক: [{string.Join(", ", missingMandatory)}].";

            if (aiSuggestedComplete)
            {
                _logger.LogWarning(
                    "⚠️ Backend OVERRODE AI completion! AI claimed conversation is complete, but ConversationState is missing mandatory facts: [{Missing}]. Conversation will continue.",
                    string.Join(", ", missingMandatory));
            }
            else
            {
                _logger.LogInformation("Backend confirmed conversation is not complete. Missing: [{Missing}]",
                    string.Join(", ", missingMandatory));
            }

            return new StateValidationDecision(
                IsAllowedToComplete: false,
                OverriddenByBackend: aiSuggestedComplete,
                Reason: reason,
                MissingMandatoryFields: missingMandatory
            );
        }

        // If no mandatory fields are missing:
        // If AI also agrees it is complete, or if at least 2 turns of facts are verified
        if (aiSuggestedComplete || (aiMissingInfo != null && aiMissingInfo.IsInformationSufficient))
        {
            _logger.LogInformation("Backend validated and APPROVED conversation completion. All mandatory legal facts verified in ConversationState.");

            return new StateValidationDecision(
                IsAllowedToComplete: true,
                OverriddenByBackend: false,
                Reason: "প্রয়োজনীয় সকল তথ্য সংগ্রহ সম্পন্ন হয়েছে এবং যাচাই করা হয়েছে।",
                MissingMandatoryFields: []
            );
        }

        // AI did not suggest complete and turns remain: keep going
        return new StateValidationDecision(
            IsAllowedToComplete: false,
            OverriddenByBackend: false,
            Reason: "অতিরিক্ত তথ্য সংগ্রহ চলমান রয়েছে।",
            MissingMandatoryFields: []
        );
    }

    /// <summary>
    /// Checks whether the user explicitly conveyed their wish to stop, cancel,
    /// or conclude the conversation (in Bangla or English).
    /// </summary>
    public bool IsExplicitStopRequested(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var clean = text.Trim().ToLowerInvariant()
            .Replace("।", "")
            .Replace("?", "")
            .Replace("!", "")
            .Replace(",", "")
            .Replace(".", "");

        // 1. Exact or short English stop signals
        string[] englishStopSignals =
        [
            "stop",
            "cancel",
            "quit",
            "end",
            "exit",
            "done",
            "finish",
            "finished",
            "no more",
            "no more questions",
            "no more info",
            "no more information",
            "that is all",
            "that's all",
            "stop asking",
            "end conversation",
            "file case",
            "submit case",
            "submit complaint",
            "file complaint"
        ];

        foreach (var signal in englishStopSignals)
        {
            if (clean == signal || clean.StartsWith(signal + " ", StringComparison.OrdinalIgnoreCase) || clean.EndsWith(" " + signal, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Explicit stop detected via English signal: '{Signal}' in text: '{Text}'", signal, text);
                return true;
            }
        }

        // 2. Bangla explicit stop keywords & phrases
        string[] banglaStopPhrases =
        [
            "আর কিছু বলতে চাই না",
            "আর বলতে চাই না",
            "বলতে চাই না",
            "আর কিছু বলব না",
            "আর বলব না",
            "আর কিছু বলার নেই",
            "আর বলার নেই",
            "বলার নেই",
            "আর কিছু জানাতে চাই না",
            "আর জানাতে চাই না",
            "আর কিছু বলতে পারব না",
            "আর বলতে পারব না",
            "আর কোনো তথ্য নেই",
            "আর তথ্য নেই",
            "আর কিছু নেই",
            "তথ্য নেই",
            "সব বলেছি",
            "সব বলে দিয়েছি",
            "সব বলে ফেলেছি",
            "আর কিছু জানা নেই",
            "যা জানার জেনেছেন",
            "থামুন",
            "বন্ধ করুন",
            "সমাপ্ত করুন",
            "শেষ করুন",
            "এখানেই শেষ",
            "এখানেই শেষ করতে চাই",
            "কথোপকথন শেষ করুন",
            "শেষ করতে চাই",
            "বাতিল করুন",
            "আর প্রশ্ন করবেন না",
            "প্রশ্ন করা বন্ধ করুন",
            "প্রশ্ন আর করবেন না",
            "আর কোনো প্রশ্ন করবেন না",
            "প্রশ্ন করবেন না",
            "আর জিজ্ঞেস করবেন না",
            "মামলা করুন",
            "কেস ফাইল করুন",
            "মামলা দায়ের করুন",
            "অভিযোগ জমা দিন",
            "আবেদন জমা দিন",
            "মামলা জমা দিন",
            "ফাইল করুন",
            "জমা দিন",
            "কেস দিন"
        ];

        foreach (var phrase in banglaStopPhrases)
        {
            if (clean.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Explicit stop detected via Bangla phrase: '{Phrase}' in text: '{Text}'", phrase, text);
                return true;
            }
        }

        return false;
    }

    private static bool IsGenericPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        var placeholders = new[] { "ভুক্তভোগী", "ভুক্তভোগী নাগরিক", "নাগরিক", "বাদী", "আবেদনকারী", "অভিযোগকারী", "বিবাদী", "অভিযুক্ত", "প্রতিপক্ষ", "মালিক", "অজ্ঞাত", "অজ্ঞাতনামা", "তথ্য পাওয়া যায়নি" };
        return placeholders.Any(p => trimmed.Equals(p, StringComparison.OrdinalIgnoreCase));
    }
}
