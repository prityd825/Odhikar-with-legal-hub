using Microsoft.Extensions.Options;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Extensible question category manager and backend flow controller.
/// Merges built-in category definitions with configuration from appsettings.json.
/// Enforces priority, checks collected facts, prevents duplicate questions, and controls the dialogue progression.
/// </summary>
public sealed class QuestionCategoryService : IQuestionCategoryService
{
    private readonly IQuestionBankService _questionBank;
    private readonly QuestionCategorySettings _settings;
    private readonly ILogger<QuestionCategoryService> _logger;
    private readonly List<QuestionCategoryDefinition> _categories;

    public QuestionCategoryService(
        IQuestionBankService questionBank,
        IOptions<QuestionCategorySettings> options,
        ILogger<QuestionCategoryService> logger)
    {
        _questionBank = questionBank;
        _settings = options.Value ?? new QuestionCategorySettings();
        _logger = logger;
        _categories = BuildCategoryRegistry(_settings.Categories);
    }

    public IReadOnlyList<QuestionCategoryDefinition> GetAllCategories() =>
        _categories.Where(c => c.IsEnabled).OrderBy(c => c.Priority).ToList();

    public QuestionCategoryDefinition? GetCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return null;
        return _categories.FirstOrDefault(c =>
            c.IsEnabled && string.Equals(c.Id, categoryId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<QuestionCategoryDefinition> GetApplicableCategories(string? incidentType)
    {
        var all = GetAllCategories();
        if (string.IsNullOrWhiteSpace(incidentType)) return all;

        return all.Where(c =>
            c.ApplicableIncidentTypes.Count == 0 ||
            c.ApplicableIncidentTypes.Any(t => incidentType.Contains(t, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    public string GetCategoryPromptDescriptions()
    {
        var active = GetAllCategories();
        return string.Join("\n", active.Select(c =>
            $"- \"{c.Id}\": {c.DisplayName} ({c.Description}). Example: \"{c.DefaultQuestionBangla}\""));
    }

    public async Task<QuestionCategoryDecision> DecideNextQuestionAsync(
        string? suggestedCategoryCode,
        string? suggestedQuestionBangla,
        ConversationState state,
        string? incidentType,
        ExtractedLegalData? extractedData,
        string? userContextText = null,
        CancellationToken ct = default)
    {
        var applicable = GetApplicableCategories(incidentType);

        // Normalize suggested category code
        var normalizedSuggestedCode = NormalizeCategoryCode(suggestedCategoryCode);
        var suggestedCategory = applicable.FirstOrDefault(c =>
            string.Equals(c.Id, normalizedSuggestedCode, StringComparison.OrdinalIgnoreCase));

        // 1. Check if LLM suggested an appropriate, unfulfilled category
        if (suggestedCategory != null && IsCategoryNeeded(suggestedCategory, state))
        {
            var (questionText, source) = await ResolveQuestionTextAsync(
                suggestedCategory, suggestedQuestionBangla, state, incidentType, userContextText, ct);

            _logger.LogInformation("Backend accepted LLM-suggested category '{CategoryId}' (Source: {Source})",
                suggestedCategory.Id, source);

            return new QuestionCategoryDecision(
                CategoryId: suggestedCategory.Id,
                CategoryName: suggestedCategory.DisplayName,
                QuestionBangla: questionText,
                SelectionSource: source,
                IsIntakeComplete: false,
                Reasoning: $"LLM suggestion accepted for uncollected category: {suggestedCategory.DisplayName}"
            );
        }

        // 2. If suggested category was rejected, already collected, or missing, evaluate by priority order
        if (suggestedCategory != null)
        {
            _logger.LogInformation("LLM suggested category '{CategoryId}' was already satisfied or previously asked. Overriding via backend priority flow.",
                suggestedCategory.Id);
        }

        foreach (var category in applicable.OrderBy(c => c.Priority))
        {
            if (IsCategoryNeeded(category, state))
            {
                var (questionText, source) = await ResolveQuestionTextAsync(
                    category, null, state, incidentType, userContextText, ct);

                _logger.LogInformation("Backend priority flow selected category '{CategoryId}' (Priority: {Priority}, Source: {Source})",
                    category.Id, category.Priority, source);

                return new QuestionCategoryDecision(
                    CategoryId: category.Id,
                    CategoryName: category.DisplayName,
                    QuestionBangla: questionText,
                    SelectionSource: source,
                    IsIntakeComplete: false,
                    Reasoning: $"Priority flow selected highest remaining unaddressed category: {category.DisplayName}"
                );
            }
        }

        // 3. All applicable categories have been addressed
        _logger.LogInformation("All applicable question categories have been addressed for conversation {ConversationId}. Intake complete.",
            state.ConversationId);

        return new QuestionCategoryDecision(
            CategoryId: "completed",
            CategoryName: "Intake Complete",
            QuestionBangla: "ধন্যবাদ! আপনার সকল তথ্য বিস্তারিতভাবে সংরক্ষণ করা হয়েছে। আমাদের আইনি টিম অভিযোগটি পর্যালোচনা করবে।",
            SelectionSource: "FlowCompletion",
            IsIntakeComplete: true,
            Reasoning: "All mandatory and applicable categories have been collected."
        );
    }

    private bool IsCategoryNeeded(QuestionCategoryDefinition category, ConversationState state)
    {
        // Has this category already been asked?
        if (state.AskedQuestionCategories.Contains(category.Id, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Has the mapped fact already been collected?
        if (!string.IsNullOrWhiteSpace(category.RequiredFactKey) &&
            state.InformationAlreadyCollected.TryGetValue(category.RequiredFactKey, out var factValue) &&
            !string.IsNullOrWhiteSpace(factValue))
        {
            return false;
        }

        return true;
    }

    private async Task<(string Text, string Source)> ResolveQuestionTextAsync(
        QuestionCategoryDefinition category,
        string? suggestedQuestion,
        ConversationState state,
        string? incidentType,
        string? userContextText,
        CancellationToken ct)
    {
        // 1. LLM Wording if allowed and non-duplicate
        if (_settings.AllowLlmQuestionWording &&
            !string.IsNullOrWhiteSpace(suggestedQuestion) &&
            !IsDuplicateQuestion(suggestedQuestion, state))
        {
            return (suggestedQuestion.Trim(), "LlmSuggested");
        }

        // 2. Context-aware selection from MongoDB Question Bank
        try
        {
            var bankQuestion = await _questionBank.SelectContextualQuestionAsync(
                category.Id, state, incidentType, userContextText, ct);

            if (bankQuestion != null && !string.IsNullOrWhiteSpace(bankQuestion.QuestionBangla))
            {
                return (bankQuestion.QuestionBangla, $"QuestionBank:{bankQuestion.QuestionId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch question from Question Bank for category '{Category}'. Falling back to default.", category.Id);
        }

        // 3. Fallback to pre-approved category default
        return (category.DefaultQuestionBangla, "CategoryDefault");
    }

    private static bool IsDuplicateQuestion(string candidate, ConversationState state)
    {
        var norm = NormalizeQuestionText(candidate);
        foreach (var asked in state.QuestionsAlreadyAsked)
        {
            var askedNorm = NormalizeQuestionText(asked);
            if (askedNorm == norm || (norm.Length > 12 && (askedNorm.Contains(norm) || norm.Contains(askedNorm))))
            {
                return true;
            }
        }
        return false;
    }

    private static string NormalizeQuestionText(string text) =>
        text.Trim()
            .Replace("?", "")
            .Replace("।", "")
            .Replace("!", "")
            .Replace(",", "")
            .Replace(" ", "");

    private static string NormalizeCategoryCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var trimmed = code.Trim().ToLowerInvariant();

        // Handle legacy or mapped aliases
        return trimmed switch
        {
            "identity" or "respondent" or "respondent_name" or "accused" => "perpetrator",
            "timeline" or "date" or "time" or "incidentdate" => "incident_time",
            "location" or "address" or "incidentlocation" => "incident_location",
            "financial" or "amount" or "dower" or "wages" or "money" => "financial_details",
            "evidence" or "documents" or "proof" => "evidence_documents",
            "witness" or "witnesses" => "witness_details",
            "safety" => "current_safety",
            "threat" or "threats" => "threat_details",
            "injury" or "injuries" or "medical" => "injury_details",
            "description" or "facts" => "incident_description",
            "relief" or "remedy" => "desired_relief",
            _ => trimmed
        };
    }

    private static List<QuestionCategoryDefinition> BuildCategoryRegistry(List<QuestionCategoryDefinition> custom)
    {
        var builtIn = GetDefaultCategories();
        var map = new Dictionary<string, QuestionCategoryDefinition>(StringComparer.OrdinalIgnoreCase);

        // 1. Load built-in defaults
        foreach (var def in builtIn)
        {
            map[def.Id] = def;
        }

        // 2. Apply custom overrides or new categories from appsettings.json
        foreach (var c in custom)
        {
            if (!string.IsNullOrWhiteSpace(c.Id))
            {
                map[c.Id] = c;
            }
        }

        return map.Values.OrderBy(c => c.Priority).ToList();
    }

    private static List<QuestionCategoryDefinition> GetDefaultCategories() =>
    [
        new QuestionCategoryDefinition
        {
            Id = "applicant",
            DisplayName = "Applicant / Victim Name",
            Description = "Identifies the applicant or victim's full name",
            Priority = 5,
            RequiredFactKey = "applicant",
            DefaultQuestionBangla = "আপনার পূর্ণ নাম কী?",
            IsMandatory = true,
            Group = "CoreFacts"
        },
        new QuestionCategoryDefinition
        {
            Id = "current_safety",
            DisplayName = "Current Safety",
            Description = "Assesses immediate physical danger, fear, or need for safe shelter",
            Priority = 10,
            RequiredFactKey = "current_safety",
            DefaultQuestionBangla = "আপনি বর্তমানে কি কোনো নিরাপদ আশ্রয়ে আছেন, নাকি কোনো তাৎক্ষণিক বিপদের ঝুঁকি রয়েছে?",
            IsMandatory = false,
            Group = "Safety"
        },
        new QuestionCategoryDefinition
        {
            Id = "threat_details",
            DisplayName = "Threat Details",
            Description = "Gathers details of explicit verbal threats, intimidation, or coercion",
            Priority = 20,
            RequiredFactKey = "threat_details",
            DefaultQuestionBangla = "আপনাকে কি কোনো সুনির্দিষ্ট হুমকি দেওয়া হয়েছে? কখন বা কীভাবে এই হুমকি দেওয়া হয়েছিল?",
            IsMandatory = false,
            Group = "Safety"
        },
        new QuestionCategoryDefinition
        {
            Id = "injury_details",
            DisplayName = "Injury Details",
            Description = "Records physical harm, assault injuries, or medical treatment taken",
            Priority = 30,
            RequiredFactKey = "injury_details",
            DefaultQuestionBangla = "আপনার বা পরিবারের কারো কি কোনো শারীরিক আঘাত বা জখম হয়েছে? কোনো চিকিৎসা নেওয়া হয়েছিল কি?",
            IsMandatory = false,
            Group = "Safety"
        },
        new QuestionCategoryDefinition
        {
            Id = "perpetrator",
            DisplayName = "Perpetrator / Respondent",
            Description = "Identifies the accused party, husband, employer, or opposing entity",
            Priority = 40,
            RequiredFactKey = "respondent",
            DefaultQuestionBangla = "অভিযুক্ত বা অপর পক্ষের নাম, পরিচয় ও বর্তমান ঠিকানা কি জানা আছে?",
            IsMandatory = true,
            Group = "CoreFacts"
        },
        new QuestionCategoryDefinition
        {
            Id = "incident_description",
            DisplayName = "Incident Description",
            Description = "Captures the core narrative of what transpired",
            Priority = 50,
            RequiredFactKey = "key_facts",
            DefaultQuestionBangla = "ঘটনাটি বিস্তারিতভাবে বলুন—কীভাবে বিরোধটি শুরু হয়েছিল এবং সেখানে কী ঘটেছিল?",
            IsMandatory = true,
            Group = "CoreFacts"
        },
        new QuestionCategoryDefinition
        {
            Id = "incident_time",
            DisplayName = "Incident Time",
            Description = "Establishes timeline, dates of incident, marriage, or eviction",
            Priority = 60,
            RequiredFactKey = "incident_date",
            DefaultQuestionBangla = "ঘটনাটি কোন তারিখে, আনুমানিক কোন সময়ে বা কতদিন আগে ঘটেছিল?",
            IsMandatory = true,
            Group = "CoreFacts"
        },
        new QuestionCategoryDefinition
        {
            Id = "incident_location",
            DisplayName = "Incident Location",
            Description = "Identifies district, police station (thana), and precise venue",
            Priority = 70,
            RequiredFactKey = "incident_location",
            DefaultQuestionBangla = "ঘটনাটি কোন জেলা, থানা বা সুনির্দিষ্ট কোন স্থানে সংঘটিত হয়েছিল?",
            IsMandatory = false,
            Group = "CoreFacts"
        },
        new QuestionCategoryDefinition
        {
            Id = "financial_details",
            DisplayName = "Financial / Claim Details",
            Description = "Quantifies monetary claims such as dower, maintenance, or unpaid wages",
            Priority = 80,
            RequiredFactKey = "disputed_amount",
            DefaultQuestionBangla = "দাবিকৃত বা ক্ষতির আর্থিক পরিমাণ (যেমন দেনমোহর বা বকেয়া বেতন) আনুমানিক কত টাকা?",
            IsMandatory = false,
            Group = "Financial"
        },
        new QuestionCategoryDefinition
        {
            Id = "evidence_documents",
            DisplayName = "Evidence & Documents",
            Description = "Catalogs proofs such as kabinnama, agreements, receipts, or medical certs",
            Priority = 90,
            RequiredFactKey = "evidence",
            DefaultQuestionBangla = "আপনার কাছে কি কাবিননামা, নোটিশ, চুক্তিপত্র, রশিদ বা অন্য কোনো প্রমাণপত্র রয়েছে?",
            IsMandatory = false,
            Group = "Evidence"
        },
        new QuestionCategoryDefinition
        {
            Id = "witness_details",
            DisplayName = "Witness Details",
            Description = "Records bystanders, family, or local community witnesses",
            Priority = 100,
            RequiredFactKey = "witnesses",
            DefaultQuestionBangla = "এই ঘটনার কোনো প্রত্যক্ষদর্শী সাক্ষী বা স্থানীয় কোনো ব্যক্তি আছেন কি যিনি বিষয়টি জানেন?",
            IsMandatory = false,
            Group = "Evidence"
        },
        new QuestionCategoryDefinition
        {
            Id = "desired_relief",
            DisplayName = "Desired Relief",
            Description = "Clarifies the legal remedy, mediation, or legal aid desired",
            Priority = 110,
            RequiredFactKey = "desired_relief",
            DefaultQuestionBangla = "এই আইনি প্রক্রিয়ায় আপনি মূলত কী প্রতিকার বা সমাধান চান (যেমন: পাওনা আদায়, নিরাপত্তা, নাকি সমঝোতা)?",
            IsMandatory = false,
            Group = "Relief"
        }
    ];
}
