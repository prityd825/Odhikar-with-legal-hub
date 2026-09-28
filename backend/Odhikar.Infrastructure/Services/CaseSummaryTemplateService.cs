using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Domain service implementing dynamic case summary template retrieval and
/// evaluation against live case and conversation data.
/// </summary>
public sealed class CaseSummaryTemplateService : ICaseSummaryTemplateService
{
    private readonly MongoDbContext _context;
    private readonly IConversationAnswerService _answerService;
    private readonly ILogger<CaseSummaryTemplateService> _logger;

    public CaseSummaryTemplateService(
        MongoDbContext context,
        IConversationAnswerService answerService,
        ILogger<CaseSummaryTemplateService> logger)
    {
        _context = context;
        _answerService = answerService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CaseSummaryTemplateDto>> GetTemplatesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var filter = activeOnly
            ? Builders<CaseSummaryTemplateModel>.Filter.Eq(x => x.IsActive, true)
            : Builders<CaseSummaryTemplateModel>.Filter.Empty;

        var items = await _context.CaseSummaryTemplates
            .Find(filter)
            .SortBy(x => x.ParentCategoryCode)
            .ToListAsync(ct);

        return items.Select(CaseSummaryTemplateDto.From).ToList();
    }

    public async Task<CaseSummaryTemplateDto?> GetTemplateByParentCategoryAsync(string parentCategoryCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parentCategoryCode)) return null;

        var resolvedCode = ResolveParentCategoryCode(parentCategoryCode);
        var template = await _context.CaseSummaryTemplates
            .Find(x => x.ParentCategoryCode == resolvedCode && x.IsActive)
            .SortByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        return template == null ? null : CaseSummaryTemplateDto.From(template);
    }

    public async Task<CaseSummaryTemplateDto?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _context.CaseSummaryTemplates
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(ct);

        return template == null ? null : CaseSummaryTemplateDto.From(template);
    }

    public async Task<CaseSummaryResultDto?> BuildSummaryFromCaseAsync(CaseModel caseModel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caseModel);

        var resolvedParentCode = ResolveParentCategoryCode(caseModel.Category);
        var template = await _context.CaseSummaryTemplates
            .Find(x => x.ParentCategoryCode == resolvedParentCode && x.IsActive)
            .SortByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        if (template == null)
        {
            // Fallback to OTHER if specific template is not found
            template = await _context.CaseSummaryTemplates
                .Find(x => x.ParentCategoryCode == "OTHER" && x.IsActive)
                .SortByDescending(x => x.Version)
                .FirstOrDefaultAsync(ct);

            if (template == null) return null;
        }

        var parentCategory = await _context.ParentIncidentCategories
            .Find(x => x.Code == template.ParentCategoryCode)
            .FirstOrDefaultAsync(ct);

        var parentCategoryName = parentCategory?.Name ?? template.Name;

        var answerDict = await _answerService.GetAnswerDictionaryAsync(null, null, caseModel.Id, ct);
        var fieldResults = new List<CaseSummaryFieldResultDto>();

        foreach (var field in template.Fields.OrderBy(f => f.Order))
        {
            var result = EvaluateFieldFromCase(field, caseModel, answerDict);
            fieldResults.Add(result);
        }

        var requiredFields = fieldResults.Where(f => f.IsRequired).ToList();
        var completedRequired = requiredFields.Count(f => f.Status != SummaryFieldStatus.Missing);
        var missingRequiredKeys = requiredFields.Where(f => f.Status == SummaryFieldStatus.Missing).Select(f => f.Key).ToList();
        var completionRate = requiredFields.Count > 0
            ? Math.Round((double)completedRequired / requiredFields.Count * 100.0, 1)
            : 100.0;

        return new CaseSummaryResultDto
        {
            CaseId = caseModel.Id.ToString(),
            CaseNumber = caseModel.CaseNumber,
            ConversationId = null,
            ParentCategoryCode = template.ParentCategoryCode,
            ParentCategoryName = parentCategoryName,
            TemplateVersion = template.Version,
            CompletionRate = completionRate,
            TotalRequiredFields = requiredFields.Count,
            CompletedRequiredFields = completedRequired,
            MissingRequiredCount = missingRequiredKeys.Count,
            Fields = fieldResults,
            MissingRequiredKeys = missingRequiredKeys,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<CaseSummaryResultDto?> BuildSummaryFromConversationAsync(Conversation conversation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var category = conversation.Analysis.Category ?? conversation.IncidentType;
        var resolvedParentCode = ResolveParentCategoryCode(category);

        var template = await _context.CaseSummaryTemplates
            .Find(x => x.ParentCategoryCode == resolvedParentCode && x.IsActive)
            .SortByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);

        if (template == null)
        {
            template = await _context.CaseSummaryTemplates
                .Find(x => x.ParentCategoryCode == "OTHER" && x.IsActive)
                .SortByDescending(x => x.Version)
                .FirstOrDefaultAsync(ct);

            if (template == null) return null;
        }

        var parentCategory = await _context.ParentIncidentCategories
            .Find(x => x.Code == template.ParentCategoryCode)
            .FirstOrDefaultAsync(ct);

        var parentCategoryName = parentCategory?.Name ?? template.Name;

        var caseGuid = Guid.TryParse(conversation.CaseId, out var parsedGuid) ? parsedGuid : (Guid?)null;
        var answerDict = await _answerService.GetAnswerDictionaryAsync(conversation.Id, null, caseGuid, ct);
        var fieldResults = new List<CaseSummaryFieldResultDto>();

        foreach (var field in template.Fields.OrderBy(f => f.Order))
        {
            var result = EvaluateFieldFromConversation(field, conversation, answerDict);
            fieldResults.Add(result);
        }

        var requiredFields = fieldResults.Where(f => f.IsRequired).ToList();
        var completedRequired = requiredFields.Count(f => f.Status != SummaryFieldStatus.Missing);
        var missingRequiredKeys = requiredFields.Where(f => f.Status == SummaryFieldStatus.Missing).Select(f => f.Key).ToList();
        var completionRate = requiredFields.Count > 0
            ? Math.Round((double)completedRequired / requiredFields.Count * 100.0, 1)
            : 100.0;

        return new CaseSummaryResultDto
        {
            CaseId = conversation.CaseId,
            CaseNumber = null,
            ConversationId = conversation.Id.ToString(),
            ParentCategoryCode = template.ParentCategoryCode,
            ParentCategoryName = parentCategoryName,
            TemplateVersion = template.Version,
            CompletionRate = completionRate,
            TotalRequiredFields = requiredFields.Count,
            CompletedRequiredFields = completedRequired,
            MissingRequiredCount = missingRequiredKeys.Count,
            Fields = fieldResults,
            MissingRequiredKeys = missingRequiredKeys,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static CaseSummaryFieldResultDto EvaluateFieldFromCase(
        CaseSummaryTemplateField field,
        CaseModel caseModel,
        Dictionary<string, string> answerDict)
    {
        // 1. Candidate lookup keys (canonical key first, then source question codes and aliases)
        var candidateKeys = BuildCandidateKeys(field);

        // 2. Check caseModel.ExtractedData
        foreach (var key in candidateKeys)
        {
            if (caseModel.ExtractedData.TryGetValue(key, out var extracted) && !string.IsNullOrWhiteSpace(extracted.Value))
            {
                var isInferred = extracted.IsInferred;
                var confidence = isInferred ? Math.Round(caseModel.CategoryConfidence > 0 ? caseModel.CategoryConfidence : 0.85, 2) : 1.0;

                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = isInferred ? SummaryFieldStatus.Inferred : SummaryFieldStatus.Stated,
                    Value = extracted.Value.Trim(),
                    IsInferred = isInferred,
                    InferenceNote = extracted.InferenceNote ?? (isInferred ? field.DefaultInferenceNote : null),
                    Confidence = confidence,
                    SourceKey = key
                };
            }
        }

        // 3. Check normalized answer dictionary from conversation_answers
        foreach (var key in candidateKeys)
        {
            if (answerDict.TryGetValue(key, out var answerVal) && !string.IsNullOrWhiteSpace(answerVal))
            {
                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = SummaryFieldStatus.Stated,
                    Value = answerVal.Trim(),
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = $"Answer:{key}"
                };
            }
        }

        // 4. Special handling for safety/threat fields
        if (field.Key is "immediateThreat" or "safetyRisk")
        {
            if (caseModel.IsSafetyEscalated)
            {
                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = SummaryFieldStatus.Stated,
                    Value = caseModel.SafetyReason ?? "জরুরি নিরাপত্তা ঝুঁকি শনাক্ত হয়েছে (১৬৪৩০ / ৯৯৯)",
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = "isSafetyEscalated"
                };
            }
        }

        // 4b. Check narrative / statement / details from client voice transcript or dialogue history
        if (field.Key is "incidentDetails" or "clientStatement" or "violenceDetails" or "threatDetails" or "disputeDescription" or "facts" or "jobDetails" or "landDetails" or "harassmentNature")
        {
            var transcript = caseModel.DialogueHistory.LastOrDefault(d => !string.IsNullOrWhiteSpace(d.Transcript))?.Transcript;
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = SummaryFieldStatus.Stated,
                    Value = transcript.Trim(),
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = "DialogueHistory.Transcript"
                };
            }
        }

        // 5. Missing field
        return new CaseSummaryFieldResultDto
        {
            Key = field.Key,
            LabelBn = field.LabelBn,
            LabelEn = field.LabelEn,
            Order = field.Order,
            IsRequired = field.IsRequired,
            FieldType = field.FieldType,
            Status = SummaryFieldStatus.Missing,
            Value = null,
            IsInferred = false,
            InferenceNote = null,
            Confidence = null,
            SourceKey = null
        };
    }

    private static CaseSummaryFieldResultDto EvaluateFieldFromConversation(
        CaseSummaryTemplateField field,
        Conversation conversation,
        Dictionary<string, string> answerDict)
    {
        var candidateKeys = BuildCandidateKeys(field);

        // 1. Check conversation.State.InformationAlreadyCollected FIRST (accurate turn-by-turn facts)
        if (conversation.State?.InformationAlreadyCollected != null)
        {
            foreach (var key in candidateKeys)
            {
                if (conversation.State.InformationAlreadyCollected.TryGetValue(key, out var collectedVal) && !string.IsNullOrWhiteSpace(collectedVal) && !IsGenericPlaceholder(collectedVal))
                {
                    return new CaseSummaryFieldResultDto
                    {
                        Key = field.Key,
                        LabelBn = field.LabelBn,
                        LabelEn = field.LabelEn,
                        Order = field.Order,
                        IsRequired = field.IsRequired,
                        FieldType = field.FieldType,
                        Status = SummaryFieldStatus.Stated,
                        Value = collectedVal.Trim(),
                        IsInferred = false,
                        InferenceNote = null,
                        Confidence = 1.0,
                        SourceKey = key
                    };
                }
            }
        }

        // 2. Check normalized answer dictionary from conversation_answers
        foreach (var key in candidateKeys)
        {
            if (answerDict.TryGetValue(key, out var answerVal) && !string.IsNullOrWhiteSpace(answerVal) && !IsGenericPlaceholder(answerVal))
            {
                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = SummaryFieldStatus.Stated,
                    Value = answerVal.Trim(),
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = $"Answer:{key}"
                };
            }
        }

        // 3. Fallback: Check structured ExtractedData if present
        var extData = conversation.Analysis?.ExtractedData ?? conversation.ExtractedData;
        if (extData != null)
        {
            InferredField? match = null;

            var normKey = field.Key.Replace("_", "").ToLowerInvariant();
            if (field.Key.Equals("applicant", StringComparison.OrdinalIgnoreCase)) match = extData.Applicant;
            else if (field.Key.Equals("respondent", StringComparison.OrdinalIgnoreCase) || field.Key.Equals("employerName", StringComparison.OrdinalIgnoreCase)) match = extData.Respondent;
            else if (normKey is "claimedamount" or "disputedamount" or "disputed_amount" or "dowertotal" or "doweramount" or "duewageamount")
            {
                if (!normKey.Contains("paid") && !normKey.Contains("due")) match = extData.DisputedAmount;
            }
            else if (normKey is "marriagedate" or "incidentdate") match = extData.MarriageOrIncidentDate;

            if (match != null && !string.IsNullOrWhiteSpace(match.Value) && !IsGenericPlaceholder(match.Value))
            {
                var confidence = match.IsInferred ? Math.Round(conversation.Analysis?.CategoryConfidence > 0 ? conversation.Analysis.CategoryConfidence : 0.85, 2) : 1.0;
                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = match.IsInferred ? SummaryFieldStatus.Inferred : SummaryFieldStatus.Stated,
                    Value = match.Value.Trim(),
                    IsInferred = match.IsInferred,
                    InferenceNote = match.IsInferred ? field.DefaultInferenceNote : null,
                    Confidence = confidence,
                    SourceKey = "Analysis.ExtractedData"
                };
            }

            if (field.Key.Equals("evidenceList", StringComparison.OrdinalIgnoreCase) && extData.EvidenceList is { Count: > 0 })
            {
                return new CaseSummaryFieldResultDto
                {
                    Key = field.Key,
                    LabelBn = field.LabelBn,
                    LabelEn = field.LabelEn,
                    Order = field.Order,
                    IsRequired = field.IsRequired,
                    FieldType = field.FieldType,
                    Status = SummaryFieldStatus.Stated,
                    Value = string.Join(", ", extData.EvidenceList),
                    IsInferred = false,
                    InferenceNote = null,
                    Confidence = 1.0,
                    SourceKey = "Analysis.ExtractedData.EvidenceList"
                };
            }
        }

        // Safety check
        if (field.Key is "immediateThreat" or "safetyRisk" && conversation.Analysis?.IsSafetyEscalated == true)
        {
            return new CaseSummaryFieldResultDto
            {
                Key = field.Key,
                LabelBn = field.LabelBn,
                LabelEn = field.LabelEn,
                Order = field.Order,
                IsRequired = field.IsRequired,
                FieldType = field.FieldType,
                Status = SummaryFieldStatus.Stated,
                Value = conversation.Analysis.SafetyReason ?? "জরুরি নিরাপত্তা ঝুঁকি শনাক্ত হয়েছে (১৬৪৩০ / ৯৯৯)",
                IsInferred = false,
                InferenceNote = null,
                Confidence = 1.0,
                SourceKey = "isSafetyEscalated"
            };
        }

        return new CaseSummaryFieldResultDto
        {
            Key = field.Key,
            LabelBn = field.LabelBn,
            LabelEn = field.LabelEn,
            Order = field.Order,
            IsRequired = field.IsRequired,
            FieldType = field.FieldType,
            Status = SummaryFieldStatus.Missing,
            Value = null,
            IsInferred = false,
            InferenceNote = null,
            Confidence = null,
            SourceKey = null
        };
    }

    /// <summary>
    /// Normalizes raw input categories (e.g. "DOWRY", "Dower & Maintenance", "জমি বিরোধ")
    /// to the canonical 6 parent incident category codes.
    /// </summary>
    private static string ResolveParentCategoryCode(string rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return "OTHER";

        var trimmed = rawCategory.Trim().ToUpperInvariant();

        if (trimmed is "DOWER_MAINTENANCE" or "DOWRY_DOMESTIC_VIOLENCE" or "LABOUR_WAGE" or "LAND_PROPERTY" or "WOMEN_CHILD_PROTECTION" or "OTHER")
        {
            return trimmed;
        }

        // Legacy and natural language category matching
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

    private static bool IsGenericPlaceholder(string? name)
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
}
