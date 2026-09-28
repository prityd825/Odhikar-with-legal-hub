using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Orchestrates a single intake turn end-to-end: safety scan → NLU extraction →
/// field merge → categorisation → status/urgency → draft rendering → persistence.
/// Also serves the paralegal read paths with urgency-first ordering.
/// </summary>
public sealed class CaseService(
    MongoDbContext context,
    ISafetyGuardrailService safety,
    IAiExtractionService extraction,
    ITemplateEngineService templates,
    ILogger<CaseService> logger) : ICaseService
{
    private readonly MongoDbContext _context = context;
    private readonly ISafetyGuardrailService _safety = safety;
    private readonly IAiExtractionService _extraction = extraction;
    private readonly ITemplateEngineService _templates = templates;
    private readonly ILogger<CaseService> _logger = logger;

    public async Task<CaseModel> ProcessTurnAsync(
        string? caseId, string transcript, string? audioUrl, bool isVoice, CancellationToken ct = default)
    {
        var @case = await LoadOrCreateAsync(caseId, ct);

        // 1. Safety FIRST — an escalation short-circuits the automated questioning.
        var safety = await _safety.ScanAsync(transcript, ct);
        if (safety.IsTriggered)
        {
            @case.IsSafetyEscalated = true;
            @case.SafetyReason = safety.Reason;
            @case.Status = CaseStatuses.Urgent;
        }

        // 2. NLU extraction (still runs so the paralegal has structured facts).
        var extraction = await _extraction.ExtractAsync(transcript, @case.DialogueHistory, @case.ExtractedData, ct);

        MergeFields(@case, extraction);

        if (LegalCategories.IsValid(extraction.Category) && extraction.Category != LegalCategories.Unknown)
        {
            @case.Category = extraction.Category;
            @case.CategoryConfidence = extraction.CategoryConfidence;
        }

        // 3. Decide the system's response for this turn.
        var systemPrompt = safety.IsTriggered
            ? "আপনি কি এই মুহূর্তে নিরাপদ আছেন? জরুরি সহায়তার জন্য এখনই ১৬৪৩০ নম্বরে কল করুন। আপনি একা নন।"
            : extraction.NextQuestion
              ?? extraction.NarrativeSummary
              ?? "ধন্যবাদ। আপনার দেওয়া তথ্য আমরা লিপিবদ্ধ করেছি।";

        // 4. Record the dialogue turn.
        @case.DialogueHistory.Add(new DialogueTurn
        {
            TurnIndex = @case.DialogueHistory.Count,
            AudioUrl = isVoice ? audioUrl : null,
            Transcript = transcript,
            SystemPrompt = systemPrompt,
            SafetyTriggered = safety.IsTriggered
        });

        // 5. Status transition (never downgrade an urgent/escalated case).
        if (!@case.IsSafetyEscalated && @case.Status == CaseStatuses.New
            && extraction is { NeedsMoreInfo: false }
            && extraction.Category != LegalCategories.Unknown
            && extraction.Category != LegalCategories.OutOfScope)
        {
            // Enough info gathered — leave as NEW for paralegal pickup but attach a draft.
            @case.DraftComplaintText = _templates.RenderComplaint(@case);
        }
        else if (@case.IsSafetyEscalated && string.IsNullOrEmpty(@case.DraftComplaintText)
                 && LegalCategories.InScope.Contains(@case.Category))
        {
            @case.DraftComplaintText = _templates.RenderComplaint(@case);
        }

        @case.UpdatedAt = DateTime.UtcNow;

        await UpsertAsync(@case, ct);
        return @case;
    }

    public async Task<IReadOnlyList<CaseModel>> GetPrioritizedCasesAsync(CancellationToken ct = default)
    {
        var all = await _context.Cases.Find(FilterDefinition<CaseModel>.Empty).ToListAsync(ct);

        // Urgency first (escalated / URGENT), then newest.
        return all
            .OrderByDescending(c => c.IsSafetyEscalated)
            .ThenBy(c => StatusRank(c.Status))
            .ThenByDescending(c => c.CreatedAt)
            .ToList();
    }

    public async Task<CaseModel?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId))
        {
            return null;
        }
        var found = await _context.Cases.Find(c => c.Id == guidId).FirstOrDefaultAsync(ct);
        return found; // FirstOrDefaultAsync yields null when no match
    }

    public async Task<CaseModel?> UpdateDraftAsync(string id, string draftText, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId))
        {
            return null;
        }

        var update = Builders<CaseModel>.Update
            .Set(c => c.DraftComplaintText, draftText)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _context.Cases.UpdateOneAsync(c => c.Id == guidId, update, cancellationToken: ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<string> GenerateCaseNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.Cases.CountDocumentsAsync(FilterDefinition<CaseModel>.Empty, cancellationToken: ct);
        return $"ODH-{year}-{(count + 1):D4}";
    }

    public async Task<CaseModel?> UpdateStatusAsync(string id, string status, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;

        var normalizedStatus = status.Trim().ToUpperInvariant();
        var update = Builders<CaseModel>.Update
            .Set(c => c.Status, normalizedStatus)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _context.Cases.UpdateOneAsync(c => c.Id == guidId, update, cancellationToken: ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<CaseModel?> ReassignClinicSlotAsync(string id, string clinicId, DateTime scheduledSlot, CancellationToken ct = default)
    {
        if (!Guid.TryParse(id, out var guidId)) return null;

        ClinicModel? clinic = null;
        if (Guid.TryParse(clinicId, out var clinicGuid))
        {
            clinic = await _context.Clinics.Find(c => c.Id == clinicGuid).FirstOrDefaultAsync(ct);
        }
        else
        {
            clinic = await _context.Clinics.Find(c => c.Name == clinicId).FirstOrDefaultAsync(ct);
        }

        var clinicName = clinic?.Name ?? (!string.IsNullOrWhiteSpace(clinicId) ? clinicId : "ঢাকা আইনি সহায়তা কেন্দ্র");
        var clinicIdStr = clinic?.Id.ToString() ?? clinicId;

        var referral = new ClinicReferral
        {
            ClinicId = clinicIdStr,
            ClinicName = clinicName,
            ScheduledSlot = scheduledSlot == default ? DateTime.UtcNow.AddDays(2).AddHours(10) : scheduledSlot
        };

        var update = Builders<CaseModel>.Update
            .Set(c => c.ClinicReferral, referral)
            .Set(c => c.Status, CaseStatuses.Assigned)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _context.Cases.UpdateOneAsync(c => c.Id == guidId, update, cancellationToken: ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<IReadOnlyList<ClinicModel>> GetClinicsAsync(string? district = null, CancellationToken ct = default)
    {
        var filter = !string.IsNullOrWhiteSpace(district)
            ? Builders<ClinicModel>.Filter.Eq(c => c.District, district.Trim())
            : FilterDefinition<ClinicModel>.Empty;

        return await _context.Clinics.Find(filter).ToListAsync(ct);
    }

    public async Task UpdateCaseAsync(CaseModel caseModel, CancellationToken ct = default)
    {
        caseModel.UpdatedAt = DateTime.UtcNow;
        await _context.Cases.ReplaceOneAsync(c => c.Id == caseModel.Id, caseModel, cancellationToken: ct);
    }


    // --- internals -------------------------------------------------------------

    private async Task<CaseModel> LoadOrCreateAsync(string? caseId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(caseId))
        {
            var existing = await GetByIdAsync(caseId, ct);
            if (existing is not null)
            {
                return existing;
            }
        }

        return new CaseModel
        {
            CaseNumber = await GenerateCaseNumberAsync(ct),
            Status = CaseStatuses.New
        };
    }

    private static void MergeFields(CaseModel @case, ExtractionResult extraction)
    {
        foreach (var (key, field) in extraction.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            // Prefer a newly stated (non-inferred) value over an older inferred one.
            if (@case.ExtractedData.TryGetValue(key, out var current)
                && !current.IsInferred && field.IsInferred)
            {
                continue;
            }

            @case.ExtractedData[key] = field;
        }
    }

    private async Task UpsertAsync(CaseModel @case, CancellationToken ct)
    {
        await _context.Cases.ReplaceOneAsync(
            c => c.Id == @case.Id,
            @case,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    private static int StatusRank(string status) => status switch
    {
        CaseStatuses.Urgent => 0,
        CaseStatuses.New => 1,
        CaseStatuses.Assigned => 2,
        CaseStatuses.Resolved => 3,
        _ => 4
    };
}
