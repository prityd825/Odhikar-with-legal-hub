using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// MongoDB-backed question bank service providing context-aware question retrieval and ranking.
/// </summary>
public sealed class QuestionBankService(
    MongoDbContext context,
    ILogger<QuestionBankService> logger) : IQuestionBankService
{
    private readonly MongoDbContext _context = context;
    private readonly ILogger<QuestionBankService> _logger = logger;

    public async Task<IReadOnlyList<QuestionBankItem>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.QuestionBank
            .Find(q => q.IsActive)
            .SortBy(q => q.Priority)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<QuestionBankItem>> GetByCategoryAsync(
        string category,
        string? incidentType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Array.Empty<QuestionBankItem>();
        }

        var normalizedCat = category.Trim().ToLowerInvariant();
        var filter = Builders<QuestionBankItem>.Filter.And(
            Builders<QuestionBankItem>.Filter.Eq(q => q.IsActive, true),
            Builders<QuestionBankItem>.Filter.Regex(q => q.Category, new MongoDB.Bson.BsonRegularExpression($"^{normalizedCat}$", "i"))
        );

        var list = await _context.QuestionBank.Find(filter).SortBy(q => q.Priority).ToListAsync(ct);

        if (string.IsNullOrWhiteSpace(incidentType))
        {
            return list;
        }

        var incidentUpper = incidentType.ToUpperInvariant();
        return list.Where(q =>
            (q.ParentCategoryCodes ?? []).Contains("ALL", StringComparer.OrdinalIgnoreCase) ||
            (q.ParentCategoryCodes ?? []).Contains(incidentUpper, StringComparer.OrdinalIgnoreCase) ||
            (q.RelatedIncidentTypes ?? []).Count == 0 ||
            (q.RelatedIncidentTypes ?? []).Contains("ALL", StringComparer.OrdinalIgnoreCase) ||
            (q.RelatedIncidentTypes ?? []).Any(t => incidentUpper.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(incidentUpper, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    public async Task<IReadOnlyList<QuestionBankItem>> GetByParentCategoryAsync(
        string parentCategoryCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parentCategoryCode))
        {
            return Array.Empty<QuestionBankItem>();
        }

        var normalizedCode = parentCategoryCode.Trim().ToUpperInvariant();
        var filter = Builders<QuestionBankItem>.Filter.And(
            Builders<QuestionBankItem>.Filter.Eq(q => q.IsActive, true),
            Builders<QuestionBankItem>.Filter.Or(
                Builders<QuestionBankItem>.Filter.AnyEq(q => q.ParentCategoryCodes, "ALL"),
                Builders<QuestionBankItem>.Filter.AnyEq(q => q.ParentCategoryCodes, normalizedCode),
                Builders<QuestionBankItem>.Filter.AnyEq(q => q.RelatedIncidentTypes, "ALL"),
                Builders<QuestionBankItem>.Filter.AnyEq(q => q.RelatedIncidentTypes, normalizedCode)
            )
        );

        return await _context.QuestionBank
            .Find(filter)
            .SortBy(q => q.Priority)
            .ToListAsync(ct);
    }

    public async Task<QuestionBankItem?> GetByQuestionIdAsync(string questionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return null;

        return await _context.QuestionBank
            .Find(q => q.QuestionId == questionId.Trim())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QuestionBankItem?> SelectContextualQuestionAsync(
        string category,
        ConversationState state,
        string? incidentType,
        string? userContextText,
        CancellationToken ct = default)
    {
        var candidates = await GetByCategoryAsync(category, incidentType, ct);
        if (candidates.Count == 0)
        {
            _logger.LogInformation("No active questions found in MongoDB question_bank for category '{Category}'", category);
            return null;
        }

        // 1. Filter out questions already asked
        var unasked = candidates.Where(c => !IsQuestionAlreadyAsked(c.QuestionBangla, state)).ToList();
        if (unasked.Count == 0)
        {
            _logger.LogInformation("All candidate questions for category '{Category}' have already been asked in this conversation.", category);
            return null;
        }

        // 2. Filter out questions whose required facts are already collected
        var eligible = unasked.Where(c =>
        {
            if (string.IsNullOrWhiteSpace(c.RequiredFactKey)) return true;
            return !state.InformationAlreadyCollected.TryGetValue(c.RequiredFactKey, out var val) || string.IsNullOrWhiteSpace(val);
        }).ToList();

        if (eligible.Count == 0)
        {
            // If all had required facts already collected, fallback to unasked
            eligible = unasked;
        }

        // 3. Score eligible candidates by context relevance & priority
        var contextCombined = BuildContextString(userContextText, state);

        var scored = eligible.Select(item =>
        {
            var score = 0;
            if (!string.IsNullOrWhiteSpace(contextCombined) && item.ContextKeywords.Count > 0)
            {
                foreach (var kw in item.ContextKeywords)
                {
                    if (contextCombined.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 15; // Context keyword match boost
                    }
                }
            }

            // Lower priority number means higher precedence
            score -= item.Priority;

            return new { Item = item, Score = score };
        })
        .OrderByDescending(s => s.Score)
        .ThenBy(s => s.Item.Priority)
        .ToList();

        var selected = scored.FirstOrDefault()?.Item;
        if (selected != null)
        {
            _logger.LogInformation("Selected question '{QuestionId}' from Question Bank for category '{Category}' (Priority: {Priority})",
                selected.QuestionId, selected.Category, selected.Priority);
        }

        return selected;
    }

    public async Task<QuestionBankItem> AddQuestionAsync(QuestionBankItem item, CancellationToken ct = default)
    {
        item.Id = Guid.NewGuid();
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(item.QuestionId))
        {
            item.QuestionId = $"Q-{item.Category.ToUpperInvariant()}-{Guid.NewGuid().ToString()[..6]}";
        }

        await _context.QuestionBank.InsertOneAsync(item, cancellationToken: ct);
        _logger.LogInformation("Added question '{QuestionId}' to MongoDB Question Bank.", item.QuestionId);
        return item;
    }

    private static bool IsQuestionAlreadyAsked(string candidateQuestion, ConversationState state)
    {
        var normCandidate = NormalizeQuestionText(candidateQuestion);
        foreach (var asked in state.QuestionsAlreadyAsked)
        {
            var normAsked = NormalizeQuestionText(asked);
            if (normAsked == normCandidate || (normCandidate.Length > 12 && (normAsked.Contains(normCandidate) || normCandidate.Contains(normAsked))))
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

    private static string BuildContextString(string? userText, ConversationState state)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(userText)) parts.Add(userText);

        if (state.InformationAlreadyCollected.TryGetValue("latest_user_update", out var update) && !string.IsNullOrWhiteSpace(update))
            parts.Add(update);

        if (state.InformationAlreadyCollected.TryGetValue("key_facts", out var facts) && !string.IsNullOrWhiteSpace(facts))
            parts.Add(facts);

        return string.Join(" ", parts);
    }
}
