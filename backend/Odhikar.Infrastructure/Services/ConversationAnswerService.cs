using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Service implementing normalized storage, indexing, and dictionary retrieval
/// for conversation and follow-up questionnaire answers.
/// </summary>
public sealed class ConversationAnswerService : IConversationAnswerService
{
    private readonly MongoDbContext _context;
    private readonly ILogger<ConversationAnswerService> _logger;

    public ConversationAnswerService(MongoDbContext context, ILogger<ConversationAnswerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveAnswerAsync(ConversationAnswerModel answer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (string.IsNullOrWhiteSpace(answer.QuestionCode))
        {
            throw new ArgumentException("QuestionCode cannot be empty.", nameof(answer));
        }

        answer.QuestionCode = answer.QuestionCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(answer.Answer) && answer.SelectedOptionTexts.Count > 0)
        {
            answer.Answer = string.Join(", ", answer.SelectedOptionTexts);
        }

        FilterDefinition<ConversationAnswerModel> filter;

        if (answer.SessionId.HasValue)
        {
            filter = Builders<ConversationAnswerModel>.Filter.Where(x =>
                x.SessionId == answer.SessionId.Value && x.QuestionCode == answer.QuestionCode);
        }
        else if (answer.ConversationId.HasValue)
        {
            filter = Builders<ConversationAnswerModel>.Filter.Where(x =>
                x.ConversationId == answer.ConversationId.Value && x.QuestionCode == answer.QuestionCode);
        }
        else if (answer.CaseId.HasValue)
        {
            filter = Builders<ConversationAnswerModel>.Filter.Where(x =>
                x.CaseId == answer.CaseId.Value && x.QuestionCode == answer.QuestionCode);
        }
        else
        {
            filter = Builders<ConversationAnswerModel>.Filter.Where(x => x.Id == answer.Id);
        }

        var existing = await _context.ConversationAnswers.Find(filter).FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            answer.Id = existing.Id;
            if (!answer.CaseId.HasValue && existing.CaseId.HasValue)
            {
                answer.CaseId = existing.CaseId;
            }
        }

        await _context.ConversationAnswers.ReplaceOneAsync(
            filter,
            answer,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken: ct);

        _logger.LogDebug("Saved normalized answer for QuestionCode: {Code}", answer.QuestionCode);
    }

    public async Task SaveAnswersAsync(IEnumerable<ConversationAnswerModel> answers, CancellationToken ct = default)
    {
        foreach (var answer in answers)
        {
            await SaveAnswerAsync(answer, ct);
        }
    }

    public async Task<IReadOnlyList<ConversationAnswerDto>> GetAnswersByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
    {
        var items = await _context.ConversationAnswers
            .Find(x => x.ConversationId == conversationId)
            .SortBy(x => x.AnsweredAt)
            .ToListAsync(ct);

        return items.Select(ConversationAnswerDto.From).ToList();
    }

    public async Task<IReadOnlyList<ConversationAnswerDto>> GetAnswersBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var items = await _context.ConversationAnswers
            .Find(x => x.SessionId == sessionId)
            .SortBy(x => x.AnsweredAt)
            .ToListAsync(ct);

        return items.Select(ConversationAnswerDto.From).ToList();
    }

    public async Task<IReadOnlyList<ConversationAnswerDto>> GetAnswersByCaseIdAsync(Guid caseId, CancellationToken ct = default)
    {
        var items = await _context.ConversationAnswers
            .Find(x => x.CaseId == caseId)
            .SortBy(x => x.AnsweredAt)
            .ToListAsync(ct);

        return items.Select(ConversationAnswerDto.From).ToList();
    }

    public async Task<Dictionary<string, string>> GetAnswerDictionaryAsync(
        Guid? conversationId,
        Guid? sessionId,
        Guid? caseId,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<ConversationAnswerModel>>();

        if (caseId.HasValue)
        {
            filters.Add(Builders<ConversationAnswerModel>.Filter.Eq(x => x.CaseId, caseId.Value));
        }

        if (sessionId.HasValue)
        {
            filters.Add(Builders<ConversationAnswerModel>.Filter.Eq(x => x.SessionId, sessionId.Value));
        }

        if (conversationId.HasValue)
        {
            filters.Add(Builders<ConversationAnswerModel>.Filter.Eq(x => x.ConversationId, conversationId.Value));
        }

        if (filters.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var combinedFilter = Builders<ConversationAnswerModel>.Filter.Or(filters);
        var answers = await _context.ConversationAnswers
            .Find(combinedFilter)
            .SortBy(x => x.AnsweredAt)
            .ToListAsync(ct);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ans in answers)
        {
            if (!string.IsNullOrWhiteSpace(ans.QuestionCode) && !string.IsNullOrWhiteSpace(ans.Answer))
            {
                dict[ans.QuestionCode] = ans.Answer;

                // Provide cross-compatible field keys alongside question codes
                MapToCommonFactKeys(ans.QuestionCode, ans.Answer, dict);
            }
        }

        return dict;
    }

    public async Task LinkCaseIdAsync(Guid sessionId, Guid caseId, CancellationToken ct = default)
    {
        var update = Builders<ConversationAnswerModel>.Update.Set(x => x.CaseId, caseId);
        await _context.ConversationAnswers.UpdateManyAsync(
            x => x.SessionId == sessionId,
            update,
            cancellationToken: ct);

        _logger.LogInformation("Linked answers from session {SessionId} to Case {CaseId}", sessionId, caseId);
    }

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        try
        {
            // Compound index: (SessionId, QuestionCode)
            var sessionCodeKeys = Builders<ConversationAnswerModel>.IndexKeys
                .Ascending(x => x.SessionId)
                .Ascending(x => x.QuestionCode);
            await _context.ConversationAnswers.Indexes.CreateOneAsync(
                new CreateIndexModel<ConversationAnswerModel>(sessionCodeKeys, new CreateIndexOptions
                {
                    Name = "ix_answers_session_question",
                    Sparse = true
                }), cancellationToken: ct);

            // Compound index: (ConversationId, QuestionCode)
            var convCodeKeys = Builders<ConversationAnswerModel>.IndexKeys
                .Ascending(x => x.ConversationId)
                .Ascending(x => x.QuestionCode);
            await _context.ConversationAnswers.Indexes.CreateOneAsync(
                new CreateIndexModel<ConversationAnswerModel>(convCodeKeys, new CreateIndexOptions
                {
                    Name = "ix_answers_conv_question",
                    Sparse = true
                }), cancellationToken: ct);

            // Index: CaseId
            var caseIdKeys = Builders<ConversationAnswerModel>.IndexKeys.Ascending(x => x.CaseId);
            await _context.ConversationAnswers.Indexes.CreateOneAsync(
                new CreateIndexModel<ConversationAnswerModel>(caseIdKeys, new CreateIndexOptions
                {
                    Name = "ix_answers_case_id",
                    Sparse = true
                }), cancellationToken: ct);

            // Index: QuestionCode
            var codeKeys = Builders<ConversationAnswerModel>.IndexKeys.Ascending(x => x.QuestionCode);
            await _context.ConversationAnswers.Indexes.CreateOneAsync(
                new CreateIndexModel<ConversationAnswerModel>(codeKeys, new CreateIndexOptions
                {
                    Name = "ix_answers_question_code"
                }), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index creation on conversation_answers checked/handled.");
        }
    }

    private static void MapToCommonFactKeys(string questionCode, string value, Dictionary<string, string> dict)
    {
        switch (questionCode)
        {
            case "INCIDENT_VICTIM":
                dict.TryAdd("applicant", value);
                break;
            case "INCIDENT_ACCUSED":
                dict.TryAdd("respondent", value);
                break;
            case "INCIDENT_TIME":
                dict.TryAdd("marriageDate", value);
                dict.TryAdd("incidentDate", value);
                break;
            case "INCIDENT_LOCATION":
                dict.TryAdd("incidentLocation", value);
                break;
            case "EVIDENCE_CHECK":
                dict.TryAdd("evidenceList", value);
                break;
            case "DOWRY_DEMAND_AMOUNT":
                dict.TryAdd("dowryDemandAmount", value);
                dict.TryAdd("claimedAmount", value);
                break;
            case "DOWRY_PAYMENT_AMOUNT":
                dict.TryAdd("dowryPaidAmount", value);
                break;
            case "DOWER_AMOUNT":
                dict.TryAdd("dowerAmount", value);
                break;
            case "DOWER_DUE_AMOUNT":
                dict.TryAdd("unpaidDowerAmount", value);
                dict.TryAdd("claimedAmount", value);
                break;
            case "MAINTENANCE_DUE_PERIOD":
                dict.TryAdd("maintenanceDue", value);
                break;
            case "CHILD_MAINTENANCE_DUE_PERIOD":
                dict.TryAdd("childCount", value);
                break;
        }
    }
}
