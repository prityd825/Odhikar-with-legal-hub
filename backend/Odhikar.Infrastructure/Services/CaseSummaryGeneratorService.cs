using System.Text;
using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Domain service that orchestrates dynamic case summary generation by matching
/// active MongoDB templates against real conversation facts and normalized answers.
/// </summary>
public sealed class CaseSummaryGeneratorService : ICaseSummaryGeneratorService
{
    private const string MissingValueText = "তথ্য পাওয়া যায়নি";

    private readonly MongoDbContext _context;
    private readonly IConversationAnswerService _answerService;
    private readonly ILogger<CaseSummaryGeneratorService> _logger;

    public CaseSummaryGeneratorService(
        MongoDbContext context,
        IConversationAnswerService answerService,
        ILogger<CaseSummaryGeneratorService> logger)
    {
        _context = context;
        _answerService = answerService;
        _logger = logger;
    }

    public async Task<GeneratedCaseSummaryDto> GenerateSummaryAsync(Guid conversationId, CancellationToken ct = default)
    {
        // 1. Get conversation
        var conversation = await _context.AiConversations
            .Find(x => x.Id == conversationId)
            .FirstOrDefaultAsync(ct);

        if (conversation == null)
        {
            throw new KeyNotFoundException($"Conversation with ID '{conversationId}' not found.");
        }

        // 2. Resolve incident category and granular incident type
        var rawCategory = conversation.Analysis.Category ?? conversation.IncidentType;
        var incidentCategoryCode = ResolveParentCategoryCode(rawCategory);

        string? incidentTypeCode = null;
        if (conversation.Analysis.RelevantIncidentTypes.Count > 0)
        {
            incidentTypeCode = conversation.Analysis.RelevantIncidentTypes[0];
        }
        else if (!string.IsNullOrWhiteSpace(conversation.IncidentType) && !conversation.IncidentType.Equals("সাধারণ আইনি সমস্যা"))
        {
            incidentTypeCode = conversation.IncidentType;
        }

        // 3. Get the appropriate active case summary template
        var template = await _context.CaseSummaryTemplates
            .Find(x => x.ParentCategoryCode == incidentCategoryCode && x.IsActive)
            .SortByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        if (template == null)
        {
            template = await _context.CaseSummaryTemplates
                .Find(x => x.ParentCategoryCode == "OTHER" && x.IsActive)
                .SortByDescending(x => x.Version)
                .FirstOrDefaultAsync(ct);

            if (template == null)
            {
                throw new InvalidOperationException($"No active summary template found for category '{incidentCategoryCode}' or fallback 'OTHER'.");
            }
        }

        // 4. Get all conversation answers & normalized fact dictionary
        var caseGuid = Guid.TryParse(conversation.CaseId, out var g) ? g : (Guid?)null;
        var answerDict = await _answerService.GetAnswerDictionaryAsync(conversationId, null, caseGuid, ct);

        // 5 & 6. Match template field keys with answer question codes and extracted facts
        var structuredFields = new List<GeneratedCaseSummaryField>();

        foreach (var field in template.Fields.OrderBy(f => f.Order))
        {
            var matchedField = EvaluateTemplateField(field, conversation, answerDict);
            structuredFields.Add(matchedField);
        }

        // 7 & 8. Evaluate completion statistics and missing required fields
        var requiredFields = structuredFields.Where(f => f.IsRequired).ToList();
        var completedRequired = requiredFields.Count(f => f.Status != "Missing");
        var missingRequiredFields = requiredFields
            .Where(f => f.Status == "Missing")
            .Select(f => f.LabelBn)
            .ToList();

        var completionRate = requiredFields.Count > 0
            ? Math.Round((double)completedRequired / requiredFields.Count * 100.0, 1)
            : 100.0;

        // 9. Synthesize professional Bangla generatedText narrative
        var generatedText = BuildGeneratedNarrativeText(
            conversation,
            template,
            incidentCategoryCode,
            incidentTypeCode,
            structuredFields,
            missingRequiredFields);

        // Persist to generated_case_summaries collection (idempotent upsert by conversationId)
        var existingSummary = await _context.GeneratedCaseSummaries
            .Find(x => x.ConversationId == conversationId)
            .FirstOrDefaultAsync(ct);

        var summaryId = existingSummary?.Id ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        var summaryModel = new GeneratedCaseSummaryModel
        {
            Id = summaryId,
            ConversationId = conversationId,
            IncidentCategoryCode = incidentCategoryCode,
            IncidentTypeCode = incidentTypeCode,
            TemplateCode = template.ParentCategoryCode,
            TemplateVersion = template.Version,
            StructuredContent = structuredFields,
            GeneratedText = generatedText,
            Status = existingSummary?.Status ?? "DRAFT",
            CompletionRate = completionRate,
            TotalRequiredFields = requiredFields.Count,
            CompletedRequiredFields = completedRequired,
            MissingRequiredFields = missingRequiredFields,
            CreatedAt = existingSummary?.CreatedAt ?? now,
            UpdatedAt = now
        };

        await _context.GeneratedCaseSummaries.ReplaceOneAsync(
            x => x.Id == summaryId,
            summaryModel,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken: ct);

        _logger.LogInformation("Generated and persisted case summary {SummaryId} for Conversation {ConversationId} ({Category})",
            summaryId, conversationId, incidentCategoryCode);

        return GeneratedCaseSummaryDto.From(summaryModel);
    }

    public async Task<GeneratedCaseSummaryDto?> GetSummaryByConversationIdAsync(Guid conversationId, CancellationToken ct = default)
    {
        var summary = await _context.GeneratedCaseSummaries
            .Find(x => x.ConversationId == conversationId)
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        return summary == null ? null : GeneratedCaseSummaryDto.From(summary);
    }

    public async Task<GeneratedCaseSummaryDto?> GetSummaryByIdAsync(Guid summaryId, CancellationToken ct = default)
    {
        var summary = await _context.GeneratedCaseSummaries
            .Find(x => x.Id == summaryId)
            .FirstOrDefaultAsync(ct);

        return summary == null ? null : GeneratedCaseSummaryDto.From(summary);
    }

    public async Task<GeneratedCaseSummaryDto?> UpdateSummaryStatusAsync(Guid summaryId, string newStatus, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            throw new ArgumentException("Status cannot be empty.", nameof(newStatus));
        }

        var normalizedStatus = newStatus.Trim().ToUpperInvariant();
        var update = Builders<GeneratedCaseSummaryModel>.Update
            .Set(x => x.Status, normalizedStatus)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _context.GeneratedCaseSummaries.FindOneAndUpdateAsync(
            x => x.Id == summaryId,
            update,
            new FindOneAndUpdateOptions<GeneratedCaseSummaryModel> { ReturnDocument = ReturnDocument.After },
            cancellationToken: ct);

        return result == null ? null : GeneratedCaseSummaryDto.From(result);
    }

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        try
        {
            var convIndex = Builders<GeneratedCaseSummaryModel>.IndexKeys.Ascending(x => x.ConversationId);
            await _context.GeneratedCaseSummaries.Indexes.CreateOneAsync(
                new CreateIndexModel<GeneratedCaseSummaryModel>(convIndex, new CreateIndexOptions
                {
                    Name = "ix_generated_summaries_conv_id"
                }), cancellationToken: ct);

            var categoryIndex = Builders<GeneratedCaseSummaryModel>.IndexKeys.Ascending(x => x.IncidentCategoryCode);
            await _context.GeneratedCaseSummaries.Indexes.CreateOneAsync(
                new CreateIndexModel<GeneratedCaseSummaryModel>(categoryIndex, new CreateIndexOptions
                {
                    Name = "ix_generated_summaries_category"
                }), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index creation on generated_case_summaries checked/handled.");
        }
    }

    private static GeneratedCaseSummaryField EvaluateTemplateField(
        CaseSummaryTemplateField field,
        Conversation conversation,
        Dictionary<string, string> answerDict)
    {
        var candidateKeys = BuildCandidateKeys(field);

        // 1. Check normalized answers dictionary from conversation_answers
        foreach (var key in candidateKeys)
        {
            if (answerDict.TryGetValue(key, out var ansVal) && !string.IsNullOrWhiteSpace(ansVal) && !IsGenericPlaceholderName(ansVal))
            {
                return new GeneratedCaseSummaryField
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Value = ansVal.Trim(),
                    Status = "Stated",
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = key
                };
            }
        }

        // 2. Check conversation.State.InformationAlreadyCollected
        foreach (var key in candidateKeys)
        {
            if (conversation.State.InformationAlreadyCollected.TryGetValue(key, out var collectedVal) && !string.IsNullOrWhiteSpace(collectedVal) && !IsGenericPlaceholderName(collectedVal))
            {
                return new GeneratedCaseSummaryField
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Value = collectedVal.Trim(),
                    Status = "Stated",
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = key
                };
            }
        }

        // 3. Check structured ExtractedData from conversation or analysis
        var ext = conversation.ExtractedData ?? conversation.Analysis?.ExtractedData;
        if (ext != null)
        {
            InferredField? match = null;

            var normKey = field.Key.Replace("_", "").ToLowerInvariant();
            if (field.Key.Equals("applicant", StringComparison.OrdinalIgnoreCase)) match = ext.Applicant;
            else if (field.Key.Equals("respondent", StringComparison.OrdinalIgnoreCase) || field.Key.Equals("employerName", StringComparison.OrdinalIgnoreCase)) match = ext.Respondent;
            else if (normKey is "claimedamount" or "disputedamount" or "disputed_amount" or "dowertotal" or "doweramount" or "duewageamount")
            {
                if (!normKey.Contains("paid") && !normKey.Contains("due")) match = ext.DisputedAmount;
            }
            else if (normKey is "marriagedate" or "incidentdate") match = ext.MarriageOrIncidentDate;

            if (match != null && !string.IsNullOrWhiteSpace(match.Value) && !IsGenericPlaceholderName(match.Value))
            {
                var isInferred = match.IsInferred;
                var confidence = isInferred
                    ? Math.Round(conversation.Analysis?.CategoryConfidence > 0 ? conversation.Analysis.CategoryConfidence : 0.85, 2)
                    : 1.0;

                return new GeneratedCaseSummaryField
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Value = match.Value.Trim(),
                    Status = isInferred ? "Inferred" : "Stated",
                    IsInferred = isInferred,
                    InferenceNote = isInferred ? (field.DefaultInferenceNote ?? "কথোপকথনের প্রাসঙ্গিক তথ্যাদি থেকে গৃহীত") : null,
                    Confidence = confidence,
                    SourceKey = "ExtractedData"
                };
            }

            if (field.Key.Equals("evidenceList", StringComparison.OrdinalIgnoreCase) && ext.EvidenceList is { Count: > 0 })
            {
                return new GeneratedCaseSummaryField
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Value = string.Join(", ", ext.EvidenceList),
                    Status = "Stated",
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = "ExtractedData.EvidenceList"
                };
            }
        }

        // 4. Emergency / Immediate threat flag
        if (field.Key is "immediateThreat" or "safetyRisk" && (conversation.IsSafetyEscalated || conversation.Analysis?.IsSafetyEscalated == true))
        {
            return new GeneratedCaseSummaryField
            {
                Key = field.Key,
                LabelBn = field.LabelBn,
                LabelEn = field.LabelEn,
                Order = field.Order,
                IsRequired = field.IsRequired,
                FieldType = field.FieldType,
                Value = conversation.SafetyReason ?? conversation.Analysis?.SafetyReason ?? "জরুরি নিরাপত্তা ঝুঁকি শনাক্ত হয়েছে (১৬৪৩০ / ৯৯৯)",
                Status = "Stated",
                IsInferred = false,
                InferenceNote = null,
                Confidence = 1.0,
                SourceKey = "isSafetyEscalated"
            };
        }

        // 5. Unavailable / Missing value -> mark as "তথ্য পাওয়া যায়নি"
        return new GeneratedCaseSummaryField
        {
            Key = field.Key,
            LabelBn = field.LabelBn,
            LabelEn = field.LabelEn,
            Order = field.Order,
            IsRequired = field.IsRequired,
            FieldType = field.FieldType,
            Value = MissingValueText,
            Status = "Missing",
            IsInferred = false,
            InferenceNote = null,
            Confidence = null,
            SourceKey = null
        };
    }

    private static string BuildGeneratedNarrativeText(
        Conversation conversation,
        CaseSummaryTemplateModel template,
        string categoryCode,
        string? incidentTypeCode,
        List<GeneratedCaseSummaryField> fields,
        List<string> missingRequiredFields)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== আইনি ঘটনার সংক্ষিপ্ত বিবরণী (Case Summary) ===");
        sb.AppendLine($"বিষয়শ্রেণী: {template.Name} [{categoryCode}]");
        if (!string.IsNullOrWhiteSpace(incidentTypeCode))
        {
            sb.AppendLine($"ঘটনার ধরন: {incidentTypeCode}");
        }

        if (conversation.Analysis.IsSafetyEscalated)
        {
            sb.AppendLine();
            sb.AppendLine("⚠️ জরুরি সতর্কতা: নিরাপত্তা ঝুঁকি পরিলক্ষিত হয়েছে।");
            if (!string.IsNullOrWhiteSpace(conversation.Analysis.SafetyReason))
            {
                sb.AppendLine($"কারণ: {conversation.Analysis.SafetyReason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("--- প্রাপ্ত তথ্যাবলি ---");
        foreach (var f in fields.Where(x => x.Status != "Missing"))
        {
            var statusNote = f.IsInferred ? " [অনুমানকৃত]" : "";
            sb.AppendLine($"• {f.LabelBn}: {f.Value}{statusNote}");
        }

        var missing = fields.Where(x => x.Status == "Missing").ToList();
        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- অসম্পূর্ণ / অপ্রাপ্ত তথ্যাবলি ---");
            foreach (var f in missing)
            {
                var reqTag = f.IsRequired ? " (আবশ্যক)" : "";
                sb.AppendLine($"• {f.LabelBn}: {MissingValueText}{reqTag}");
            }
        }

        if (conversation.Analysis.ImportantFactsMentioned.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- কথোপকথন থেকে উল্লেখযোগ্য তথ্যাংশ ---");
            foreach (var fact in conversation.Analysis.ImportantFactsMentioned.Take(5))
            {
                sb.AppendLine($"- {fact}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string ResolveParentCategoryCode(string rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return "OTHER";

        var trimmed = rawCategory.Trim().ToUpperInvariant();

        if (trimmed is "DOWER_MAINTENANCE" or "DOWRY_DOMESTIC_VIOLENCE" or "LABOUR_WAGE" or "LAND_PROPERTY" or "WOMEN_CHILD_PROTECTION" or "OTHER")
        {
            return trimmed;
        }

        if (trimmed.Contains("DOWRY") || trimmed.Contains("যৌতুক") || trimmed.Contains("সহিংসতা") || trimmed.Contains("ABUSE") || trimmed.Contains("DOMESTIC"))
        {
            return "DOWRY_DOMESTIC_VIOLENCE";
        }

        if (trimmed.Contains("DOWER") || trimmed.Contains("MAINTENANCE") || trimmed.Contains("দেনমোহর") || trimmed.Contains("ভরণপোষণ") || trimmed.Contains("খোরপোষ"))
        {
            return "DOWER_MAINTENANCE";
        }

        if (trimmed.Contains("WAGE") || trimmed.Contains("LABOUR") || trimmed.Contains("LABOR") || trimmed.Contains("মজুরি") || trimmed.Contains("শ্রম") || trimmed.Contains("বেতন"))
        {
            return "LABOUR_WAGE";
        }

        if (trimmed.Contains("LAND") || trimmed.Contains("PROPERTY") || trimmed.Contains("জমি") || trimmed.Contains("সম্পত্তি") || trimmed.Contains("উত্তরাধিকার"))
        {
            return "LAND_PROPERTY";
        }

        if (trimmed.Contains("CHILD") || trimmed.Contains("PROTECTION") || trimmed.Contains("সুরক্ষা") || trimmed.Contains("হেফাজত") || trimmed.Contains("নারী ও শিশু"))
        {
            return "WOMEN_CHILD_PROTECTION";
        }

        return "OTHER";
    }

    private static bool IsGenericPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var trimmed = name.Trim();
        var placeholders = new[] { "ভুক্তভোগী", "ভুক্তভোগী নাগরিক", "নাগরিক", "বাদী", "আবেদনকারী", "অভিযোগকারী", "বিবাদী", "অভিযুক্ত", "প্রতিপক্ষ", "মালিক", "অজ্ঞাত", "অজ্ঞাতনামা", "তথ্য পাওয়া যায়নি" };
        return placeholders.Any(p => trimmed.Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> BuildCandidateKeys(CaseSummaryTemplateField field)
    {
        var candidateKeys = new List<string> { field.Key };
        candidateKeys.AddRange(field.SourceQuestionCodes);

        var norm = field.Key.Replace("_", "").ToLowerInvariant();
        if (norm is "dowertotal" or "doweramount" or "totaldower")
        {
            candidateKeys.Add("dowerAmount"); candidateKeys.Add("dower_amount"); candidateKeys.Add("dower_total"); candidateKeys.Add("totalDower");
        }
        else if (norm is "dowerpaid" or "paiddoweramount" or "paiddower")
        {
            candidateKeys.Add("paidDowerAmount"); candidateKeys.Add("dower_paid"); candidateKeys.Add("paidDower");
        }
        else if (norm is "dowerdue" or "unpaiddoweramount" or "unpaiddower" or "duedower")
        {
            candidateKeys.Add("unpaidDowerAmount"); candidateKeys.Add("dower_due"); candidateKeys.Add("dueDower");
        }
        else if (norm is "monthlysalary" or "salary")
        {
            candidateKeys.Add("monthlySalary"); candidateKeys.Add("monthly_salary");
        }
        else if (norm is "unpaidwage" or "unpaidwageamount" or "duewageamount")
        {
            candidateKeys.Add("unpaidWageAmount"); candidateKeys.Add("dueWageAmount"); candidateKeys.Add("unpaid_wage");
        }
        else if (norm is "dowrydemand" or "dowrydemandamount")
        {
            candidateKeys.Add("dowryDemandAmount"); candidateKeys.Add("dowry_demand");
        }
        else if (norm is "marriagedate")
        {
            candidateKeys.Add("marriageDate"); candidateKeys.Add("marriage_date");
        }
        else if (norm is "incidentdate")
        {
            candidateKeys.Add("incidentDate"); candidateKeys.Add("incident_date");
        }
        else if (norm is "incidentlocation" or "district" or "location")
        {
            candidateKeys.Add("incidentLocation"); candidateKeys.Add("district"); candidateKeys.Add("location");
        }

        return candidateKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task UpdateSummaryFieldsAsync(Guid conversationId, IDictionary<string, string> fields, CancellationToken ct = default)
    {
        var existing = await _context.GeneratedCaseSummaries
            .Find(x => x.ConversationId == conversationId)
            .SortByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing == null) return;

        foreach (var (key, value) in fields)
        {
            var sf = existing.StructuredContent.FirstOrDefault(x =>
                x.Key.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                (x.SourceKey != null && x.SourceKey.Equals(key, StringComparison.OrdinalIgnoreCase)));

            if (sf != null)
            {
                var trimmedVal = value?.Trim();
                var isMissing = string.IsNullOrWhiteSpace(trimmedVal)
                    || trimmedVal.Equals("তথ্য পাওয়া যায়নি", StringComparison.OrdinalIgnoreCase)
                    || trimmedVal.Equals("জানা নেই", StringComparison.OrdinalIgnoreCase);

                sf.Value = isMissing ? MissingValueText : trimmedVal!;
                sf.Status = isMissing ? "Missing" : "Stated";
                sf.IsInferred = false;
            }
        }

        var reqFields = existing.StructuredContent.Where(x => x.IsRequired).ToList();
        var completedReq = reqFields.Count(x => x.Status != "Missing");
        existing.CompletedRequiredFields = completedReq;
        existing.CompletionRate = reqFields.Count > 0
            ? Math.Round((double)completedReq / reqFields.Count * 100.0, 1)
            : 100.0;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.GeneratedCaseSummaries.ReplaceOneAsync(
            x => x.Id == existing.Id,
            existing,
            cancellationToken: ct);
    }
}
