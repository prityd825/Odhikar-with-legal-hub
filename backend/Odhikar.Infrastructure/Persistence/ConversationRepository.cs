using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// MongoDB implementation of <see cref="IConversationRepository"/>.
/// </summary>
public sealed class ConversationRepository(MongoDbContext dbContext, ILogger<ConversationRepository> logger) : IConversationRepository
{
    private readonly MongoDbContext _dbContext = dbContext;
    private readonly ILogger<ConversationRepository> _logger = logger;

    public async Task<ConversationModel?> GetByIdAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conv = await _dbContext.Conversations
            .Find(c => c.Id == conversationId)
            .FirstOrDefaultAsync(ct);

        if (conv?.Messages != null && conv.Messages.Count > 0)
        {
            conv.Messages = conv.Messages
                .OrderBy(m => m.SequenceNumber)
                .ThenBy(m => m.Timestamp)
                .ToList();
        }

        return conv;
    }

    public async Task CreateAsync(ConversationModel conversation, CancellationToken ct = default)
    {
        await _dbContext.Conversations.InsertOneAsync(conversation, cancellationToken: ct);
        _logger.LogInformation("Created new conversation {ConversationId} for incident '{IncidentType}'",
            conversation.Id, conversation.IncidentType);
    }

    public async Task AddMessageAsync(Guid conversationId, ConversationMessage message, CancellationToken ct = default)
    {
        var update = Builders<ConversationModel>.Update
            .Push(c => c.Messages, message)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _dbContext.Conversations.UpdateOneAsync(
            c => c.Id == conversationId,
            update,
            cancellationToken: ct);

        try
        {
            await _dbContext.ConversationMessages.InsertOneAsync(message, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Standalone message collection insertion completed or skipped.");
        }
    }

    public async Task UpdateConversationAsync(ConversationModel conversation, CancellationToken ct = default)
    {
        conversation.UpdatedAt = DateTime.UtcNow;
        await _dbContext.Conversations.ReplaceOneAsync(
            c => c.Id == conversation.Id,
            conversation,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task SaveAnalysisAsync(Guid conversationId, AIAnalysis analysis, CancellationToken ct = default)
    {
        analysis.ConversationId = conversationId;
        analysis.AnalyzedAt = DateTime.UtcNow;

        // 1. Embed directly into the conversation document
        var update = Builders<ConversationModel>.Update
            .Set(c => c.Analysis, analysis)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _dbContext.Conversations.UpdateOneAsync(
            c => c.Id == conversationId,
            update,
            cancellationToken: ct);

        // 2. Also persist into the dedicated ai_analyses collection
        try
        {
            await _dbContext.AiAnalyses.InsertOneAsync(analysis, cancellationToken: ct);
            _logger.LogInformation("Saved AI analysis {AnalysisId} to ai_analyses collection for conversation {ConversationId}",
                analysis.Id, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save AI analysis to standalone ai_analyses collection for conversation {ConversationId}", conversationId);
        }
    }

    public async Task SaveStateAsync(Guid conversationId, ConversationState state, CancellationToken ct = default)
    {
        state.ConversationId = conversationId;
        state.LastActiveAt = DateTime.UtcNow;

        // 1. Embed directly into the conversation document
        var update = Builders<ConversationModel>.Update
            .Set(c => c.State, state)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _dbContext.Conversations.UpdateOneAsync(
            c => c.Id == conversationId,
            update,
            cancellationToken: ct);

        // 2. Also upsert into the dedicated conversation_states collection
        try
        {
            await _dbContext.ConversationStates.ReplaceOneAsync(
                s => s.ConversationId == conversationId,
                state,
                new ReplaceOptions { IsUpsert = true },
                ct);
            _logger.LogInformation("Saved conversation state for {ConversationId}: Stage={Stage}, Turns={Turns}, QuestionsAsked={QuestionsCount}",
                conversationId, state.CurrentStage, state.TurnCount, state.QuestionsAlreadyAsked.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save state to standalone conversation_states collection for conversation {ConversationId}", conversationId);
        }
    }

    public async Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int limit = 20, CancellationToken ct = default)
    {
        var conversation = await GetByIdAsync(conversationId, ct);
        if (conversation is null || conversation.Messages.Count == 0)
        {
            return Array.Empty<ConversationMessage>();
        }

        // Return the last 'limit' messages in strict chronological sequence
        return conversation.Messages
            .OrderBy(m => m.SequenceNumber)
            .ThenBy(m => m.Timestamp)
            .TakeLast(limit)
            .ToList();
    }

    public async Task SaveReportAsync(FinalConversationReport report, CancellationToken ct = default)
    {
        report.GeneratedAt = DateTime.UtcNow;

        // 1. Embed directly into the conversation document
        var update = Builders<ConversationModel>.Update
            .Set(c => c.FinalReport, report)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _dbContext.Conversations.UpdateOneAsync(
            c => c.Id == report.ConversationId,
            update,
            cancellationToken: ct);

        // 2. Upsert into standalone conversation_reports collection
        try
        {
            await _dbContext.ConversationReports.ReplaceOneAsync(
                r => r.ConversationId == report.ConversationId,
                report,
                new ReplaceOptions { IsUpsert = true },
                ct);
            _logger.LogInformation("Saved Final Conversation Report {ReportId} for conversation {ConversationId} (Case: {CaseNumber})",
                report.Id, report.ConversationId, report.CaseNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist report to standalone conversation_reports collection for conversation {ConversationId}", report.ConversationId);
        }
    }

    public async Task<FinalConversationReport?> GetReportByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
    {
        var report = await _dbContext.ConversationReports
            .Find(r => r.ConversationId == conversationId)
            .FirstOrDefaultAsync(ct);

        if (report != null) return report;

        // Fallback to embedded conversation report
        var conv = await _dbContext.Conversations
            .Find(c => c.Id == conversationId)
            .FirstOrDefaultAsync(ct);

        return conv?.FinalReport;
    }

    public async Task SaveFinalReportAsync(FinalReport report, CancellationToken ct = default)
    {
        report.GeneratedAt = DateTime.UtcNow;

        // 1. Embed directly into the conversation document
        var update = Builders<ConversationModel>.Update
            .Set(c => c.FinalReport, report as FinalConversationReport ?? new FinalConversationReport
            {
                Id = report.Id,
                ConversationId = report.ConversationId,
                CaseId = report.CaseId,
                CaseNumber = report.CaseNumber,
                Title = report.Title,
                Summary = report.Summary,
                IncidentTypes = report.IncidentTypes,
                ImportantFacts = report.ImportantFacts,
                Timeline = report.Timeline,
                SafetyConcerns = report.SafetyConcerns,
                MissingInformation = report.MissingInformation,
                ExtractedFacts = report.ExtractedFacts,
                Severity = report.Severity,
                SeverityReason = report.SeverityReason,
                ClientProfile = report.ClientProfile,
                OpposingPartyProfile = report.OpposingPartyProfile,
                LegalRelief = report.LegalRelief,
                PdfExport = report.PdfExport,
                FrontendDisplay = report.FrontendDisplay,
                TotalTurns = report.TotalTurns,
                CompletionReason = report.CompletionReason,
                GeneratedAt = report.GeneratedAt,
                RawReportJson = report.RawReportJson,
                Metadata = report.Metadata
            })
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _dbContext.Conversations.UpdateOneAsync(
            c => c.Id == report.ConversationId,
            update,
            cancellationToken: ct);

        // 2. Upsert into standalone final_reports collection
        try
        {
            await _dbContext.FinalReports.ReplaceOneAsync(
                r => r.ConversationId == report.ConversationId,
                report,
                new ReplaceOptions { IsUpsert = true },
                ct);
            _logger.LogInformation("Saved FinalReport {ReportId} to final_reports collection for conversation {ConversationId} (Case: {CaseNumber})",
                report.Id, report.ConversationId, report.CaseNumber);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist report to standalone final_reports collection for conversation {ConversationId}", report.ConversationId);
        }
    }

    public async Task<FinalReport?> GetFinalReportByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
    {
        var report = await _dbContext.FinalReports
            .Find(r => r.ConversationId == conversationId)
            .FirstOrDefaultAsync(ct);

        if (report != null) return report;

        return await GetReportByConversationIdAsync(conversationId, ct);
    }

    public async Task<FinalReport?> GetFinalReportByIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var report = await _dbContext.FinalReports
            .Find(r => r.Id == reportId)
            .FirstOrDefaultAsync(ct);

        if (report != null) return report;

        return await _dbContext.ConversationReports
            .Find(r => r.Id == reportId)
            .FirstOrDefaultAsync(ct);
    }
}
