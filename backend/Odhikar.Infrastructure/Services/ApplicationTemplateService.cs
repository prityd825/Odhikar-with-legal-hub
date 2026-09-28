using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Infrastructure.Services;

public sealed partial class ApplicationTemplateService : IApplicationTemplateService
{
    private readonly MongoDbContext _context;
    private readonly IConversationAnswerService _answerService;
    private readonly ICaseSummaryGeneratorService _caseSummaryGenerator;
    private readonly ILogger<ApplicationTemplateService> _logger;

    public ApplicationTemplateService(
        MongoDbContext context,
        IConversationAnswerService answerService,
        ICaseSummaryGeneratorService caseSummaryGenerator,
        ILogger<ApplicationTemplateService> logger)
    {
        _context = context;
        _answerService = answerService;
        _caseSummaryGenerator = caseSummaryGenerator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApplicationTemplateDto>> GetTemplatesAsync(
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var filter = activeOnly
            ? Builders<ApplicationTemplateModel>.Filter.Eq(x => x.IsActive, true)
            : Builders<ApplicationTemplateModel>.Filter.Empty;

        var items = await _context.ApplicationTemplates
            .Find(filter)
            .SortBy(x => x.CategoryCode)
            .ToListAsync(ct);

        return items.Select(ApplicationTemplateDto.From).ToList();
    }

    public async Task<ApplicationTemplateDto?> GetTemplateByCategoryAsync(
        string categoryCode,
        CancellationToken ct = default)
    {
        var template = await _context.ApplicationTemplates
            .Find(x => x.CategoryCode == categoryCode && x.IsActive)
            .FirstOrDefaultAsync(ct);

        return template == null ? null : ApplicationTemplateDto.From(template);
    }

    public async Task<ApplicationTemplateDto?> GetTemplateByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var template = await _context.ApplicationTemplates
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(ct);

        return template == null ? null : ApplicationTemplateDto.From(template);
    }

    /// <summary>
    /// Executes the full generator pipeline:
    /// Conversation -> Incident Category -> Generated Case Summary -> Application Template -> Placeholder Resolution -> Generated Application Draft -> Save to MongoDB.
    /// </summary>
    public async Task<GeneratedApplicationDto> GenerateDraftForConversationAsync(
        Guid conversationId,
        GenerateApplicationRequest? request = null,
        CancellationToken ct = default)
    {
        // 1. Receive conversationId and verify conversation exists
        var conversation = await _context.AiConversations
            .Find(x => x.Id == conversationId)
            .FirstOrDefaultAsync(ct);

        if (conversation == null)
        {
            throw new KeyNotFoundException($"Conversation '{conversationId}' was not found.");
        }

        // 2. Retrieve (or generate) the Case Summary
        var caseSummary = await _context.GeneratedCaseSummaries
            .Find(x => x.ConversationId == conversationId)
            .FirstOrDefaultAsync(ct);

        if (caseSummary == null)
        {
            _logger.LogInformation("No case summary found for conversation {ConvId}. Triggering CaseSummaryGenerator...", conversationId);
            var generatedSummaryDto = await _caseSummaryGenerator.GenerateSummaryAsync(conversationId, ct);
            caseSummary = await _context.GeneratedCaseSummaries
                .Find(x => x.Id == generatedSummaryDto.Id)
                .FirstOrDefaultAsync(ct);
        }

        // 3. Resolve Incident Category & Type
        var incidentCategoryCode = !string.IsNullOrWhiteSpace(request?.TargetCategoryCode)
            ? request.TargetCategoryCode
            : caseSummary?.IncidentCategoryCode ?? ResolveCategoryCode(conversation);

        var incidentTypeCode = caseSummary?.IncidentTypeCode;

        // 4. Retrieve the correct Application Template
        var template = await _context.ApplicationTemplates
            .Find(x => x.CategoryCode == incidentCategoryCode && x.IsActive)
            .FirstOrDefaultAsync(ct);

        if (template == null)
        {
            _logger.LogWarning("Template for '{Category}' not found. Falling back to 'OTHER'.", incidentCategoryCode);
            template = await _context.ApplicationTemplates
                .Find(x => x.CategoryCode == "OTHER" && x.IsActive)
                .FirstOrDefaultAsync(ct);

            if (template == null)
            {
                throw new InvalidOperationException($"No active application template found for category '{incidentCategoryCode}' or 'OTHER'.");
            }
        }

        // 5. Retrieve actual conversation answers
        var answerDict = await _answerService.GetAnswerDictionaryAsync(
            sessionId: null,
            conversationId: conversationId,
            caseId: null,
            ct: ct);

        // 6. Resolve placeholders using actual answers & case summary values (never inventing facts)
        var (placeholdersUsed, missingKeys) = BuildPlaceholderDictionary(
            template,
            conversation,
            caseSummary,
            answerDict,
            request?.ExplicitOverrides);

        // 7. Generate Bangla application draft text & subject
        var authority = ReplacePlaceholders(template.RecipientAuthorityBn, placeholdersUsed);
        var subject = ReplacePlaceholders(template.SubjectBn, placeholdersUsed);

        placeholdersUsed["RECIPIENT_AUTHORITY"] = authority;
        placeholdersUsed["SUBJECT"] = subject;

        var applicationText = ReplacePlaceholders(template.BodyTemplateBn, placeholdersUsed);

        // 8. Save result in generated_applications collection
        var existing = await _context.GeneratedApplications
            .Find(x => x.ConversationId == conversationId)
            .FirstOrDefaultAsync(ct);

        Guid? caseGuid = null;
        if (!string.IsNullOrWhiteSpace(conversation.CaseId) && Guid.TryParse(conversation.CaseId, out var parsedCase))
        {
            caseGuid = parsedCase;
        }

        GeneratedApplicationModel appModel;
        if (existing != null)
        {
            existing.CaseSummaryId = caseSummary?.Id;
            existing.IncidentCategoryCode = incidentCategoryCode;
            existing.IncidentTypeCode = incidentTypeCode;
            existing.TemplateCode = template.CategoryCode;
            existing.TemplateId = template.Id;
            existing.TemplateTitleBn = template.TitleBn;
            existing.Subject = subject;
            existing.ApplicationText = applicationText;
            existing.Status = "DRAFT";
            existing.ReviewStatus = "PENDING_REVIEW";
            existing.RecipientAuthorityBn = authority;
            existing.DisclaimerNotice = template.DisclaimerNoticeBn;
            existing.PlaceholdersUsed = placeholdersUsed;
            existing.MissingPlaceholders = missingKeys;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.GeneratedApplications.ReplaceOneAsync(
                x => x.Id == existing.Id,
                existing,
                cancellationToken: ct);

            appModel = existing;
            _logger.LogInformation("Updated generated application draft {Id} for conversation {ConvId}", appModel.Id, conversationId);
        }
        else
        {
            appModel = new GeneratedApplicationModel
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                CaseSummaryId = caseSummary?.Id,
                CaseId = caseGuid,
                IncidentCategoryCode = incidentCategoryCode,
                IncidentTypeCode = incidentTypeCode,
                TemplateCode = template.CategoryCode,
                TemplateId = template.Id,
                TemplateTitleBn = template.TitleBn,
                Subject = subject,
                ApplicationText = applicationText,
                Status = "DRAFT",
                ReviewStatus = "PENDING_REVIEW",
                RecipientAuthorityBn = authority,
                DisclaimerNotice = template.DisclaimerNoticeBn,
                PlaceholdersUsed = placeholdersUsed,
                MissingPlaceholders = missingKeys,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.GeneratedApplications.InsertOneAsync(appModel, cancellationToken: ct);
            _logger.LogInformation("Created new generated application draft {Id} for conversation {ConvId}", appModel.Id, conversationId);
        }

        return GeneratedApplicationDto.From(appModel);
    }

    public async Task<GeneratedApplicationDto?> GetGeneratedApplicationByConversationAsync(
        Guid conversationId,
        CancellationToken ct = default)
    {
        var app = await _context.GeneratedApplications
            .Find(x => x.ConversationId == conversationId)
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        return app == null ? null : GeneratedApplicationDto.From(app);
    }

    public async Task<GeneratedApplicationDto?> GetGeneratedApplicationByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var app = await _context.GeneratedApplications
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(ct);

        return app == null ? null : GeneratedApplicationDto.From(app);
    }

    public async Task<GeneratedApplicationDto?> UpdateReviewStatusAsync(
        Guid id,
        UpdateApplicationReviewStatusRequest request,
        CancellationToken ct = default)
    {
        var app = await _context.GeneratedApplications
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(ct);

        if (app == null) return null;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            app.Status = request.Status;
        }

        if (!string.IsNullOrWhiteSpace(request.ReviewStatus))
        {
            app.ReviewStatus = request.ReviewStatus;
        }

        if (!string.IsNullOrWhiteSpace(request.ReviewedBy))
        {
            app.ReviewedBy = request.ReviewedBy;
        }

        if (!string.IsNullOrWhiteSpace(request.ReviewNotes))
        {
            app.ReviewNotes = request.ReviewNotes;
        }

        app.ReviewedAt = DateTime.UtcNow;
        app.UpdatedAt = DateTime.UtcNow;

        await _context.GeneratedApplications.ReplaceOneAsync(x => x.Id == id, app, cancellationToken: ct);
        return GeneratedApplicationDto.From(app);
    }

    public async Task<GeneratedApplicationDto?> GetGeneratedApplicationByCaseIdAsync(
        Guid caseId,
        CancellationToken ct = default)
    {
        var app = await _context.GeneratedApplications
            .Find(x => x.CaseId == caseId)
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        return app == null ? null : GeneratedApplicationDto.From(app);
    }

    public async Task<GeneratedApplicationDto> GenerateDraftForCaseAsync(
        Guid caseId,
        GenerateApplicationRequest? request = null,
        CancellationToken ct = default)
    {
        var @case = await _context.Cases
            .Find(x => x.Id == caseId)
            .FirstOrDefaultAsync(ct);

        if (@case == null)
        {
            throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        }

        var incidentCategoryCode = !string.IsNullOrWhiteSpace(request?.TargetCategoryCode)
            ? request.TargetCategoryCode
            : ResolveCategoryCode(@case.Category);

        var template = await _context.ApplicationTemplates
            .Find(x => x.CategoryCode == incidentCategoryCode && x.IsActive)
            .FirstOrDefaultAsync(ct);

        if (template == null)
        {
            template = await _context.ApplicationTemplates
                .Find(x => x.CategoryCode == "OTHER" && x.IsActive)
                .FirstOrDefaultAsync(ct);

            if (template == null)
            {
                throw new InvalidOperationException($"No active application template found for '{incidentCategoryCode}' or 'OTHER'.");
            }
        }

        var answerDict = await _answerService.GetAnswerDictionaryAsync(
            sessionId: null,
            conversationId: null,
            caseId: caseId,
            ct: ct);

        var (placeholdersUsed, missingKeys) = BuildPlaceholderDictionaryForCase(
            template,
            @case,
            answerDict,
            request?.ExplicitOverrides);

        var authority = ReplacePlaceholders(template.RecipientAuthorityBn, placeholdersUsed);
        var subject = ReplacePlaceholders(template.SubjectBn, placeholdersUsed);

        placeholdersUsed["RECIPIENT_AUTHORITY"] = authority;
        placeholdersUsed["SUBJECT"] = subject;

        var applicationText = ReplacePlaceholders(template.BodyTemplateBn, placeholdersUsed);

        var existing = await _context.GeneratedApplications
            .Find(x => x.CaseId == caseId)
            .FirstOrDefaultAsync(ct);

        GeneratedApplicationModel appModel;
        if (existing != null)
        {
            existing.IncidentCategoryCode = incidentCategoryCode;
            existing.TemplateCode = template.CategoryCode;
            existing.TemplateId = template.Id;
            existing.TemplateTitleBn = template.TitleBn;
            existing.Subject = subject;
            existing.ApplicationText = applicationText;
            existing.Status = "DRAFT";
            existing.ReviewStatus = "PENDING_REVIEW";
            existing.RecipientAuthorityBn = authority;
            existing.DisclaimerNotice = template.DisclaimerNoticeBn;
            existing.PlaceholdersUsed = placeholdersUsed;
            existing.MissingPlaceholders = missingKeys;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.GeneratedApplications.ReplaceOneAsync(x => x.Id == existing.Id, existing, cancellationToken: ct);
            appModel = existing;
        }
        else
        {
            appModel = new GeneratedApplicationModel
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                IncidentCategoryCode = incidentCategoryCode,
                TemplateCode = template.CategoryCode,
                TemplateId = template.Id,
                TemplateTitleBn = template.TitleBn,
                Subject = subject,
                ApplicationText = applicationText,
                Status = "DRAFT",
                ReviewStatus = "PENDING_REVIEW",
                RecipientAuthorityBn = authority,
                DisclaimerNotice = template.DisclaimerNoticeBn,
                PlaceholdersUsed = placeholdersUsed,
                MissingPlaceholders = missingKeys,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.GeneratedApplications.InsertOneAsync(appModel, cancellationToken: ct);
        }

        return GeneratedApplicationDto.From(appModel);
    }

    public async Task<ApplicationPdfDocumentModel> BuildApplicationPdfModelForCaseAsync(
        CaseModel @case,
        CancellationToken ct = default)
    {
        // 1. Resolve parent category code from Category or ExtractedData
        var categoryCandidate = @case.Category;
        if (string.IsNullOrWhiteSpace(categoryCandidate) || categoryCandidate.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            categoryCandidate = ResolveFieldFromCase(@case, "caseType")
                ?? ResolveFieldFromCase(@case, "category")
                ?? ResolveFieldFromCase(@case, "incidentType")
                ?? ResolveFieldFromCase(@case, "parentCategory");
        }

        var parentCatCode = ResolveCategoryCode(categoryCandidate);

        // 2. Find parent category descriptor for Bengali name
        var parentCat = await _context.ParentIncidentCategories
            .Find(x => x.Code == parentCatCode)
            .FirstOrDefaultAsync(ct);

        var parentCatNameBn = parentCat?.Name ?? GetDefaultCategoryNameBn(parentCatCode);

        // 3. Retrieve the matching pre-approved application template from application_templates collection
        var template = await _context.ApplicationTemplates
            .Find(x => x.CategoryCode == parentCatCode && x.IsActive)
            .FirstOrDefaultAsync(ct);

        if (template == null)
        {
            _logger.LogWarning("Template for '{CategoryCode}' not found. Falling back to 'OTHER'.", parentCatCode);
            template = await _context.ApplicationTemplates
                .Find(x => x.CategoryCode == "OTHER" && x.IsActive)
                .FirstOrDefaultAsync(ct);
        }

        var answerDict = await _answerService.GetAnswerDictionaryAsync(
            sessionId: null,
            conversationId: null,
            caseId: @case.Id,
            ct: ct);

        // 4. Resolve placeholders strictly using case data & answers
        var (placeholdersUsed, _) = template != null
            ? BuildPlaceholderDictionaryForCase(template, @case, answerDict, null)
            : (new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new List<string>());

        var templateTitleBn = template?.TitleBn ?? $"{parentCatNameBn} আইনি আবেদনপত্র";
        var templateTitleEn = template?.TitleEn ?? string.Empty;

        var authority = template != null
            ? ReplacePlaceholders(template.RecipientAuthorityBn, placeholdersUsed)
            : "বিজ্ঞ সংশ্লিষ্ট আদালত / উপযুক্ত কর্তৃপক্ষ";

        var subject = template != null
            ? ReplacePlaceholders(template.SubjectBn, placeholdersUsed)
            : $"{parentCatNameBn} সংক্রান্ত প্রতিকার পাওয়ার আবেদন";

        var resolvedDistrict = placeholdersUsed.GetValueOrDefault("DISTRICT")
            ?? ResolveFieldFromCase(@case, "district")
            ?? "ঢাকা";

        placeholdersUsed["RECIPIENT_AUTHORITY"] = authority;
        placeholdersUsed["SUBJECT"] = subject;
        placeholdersUsed["DISTRICT"] = resolvedDistrict;
        placeholdersUsed["APPLICATION_DATE"] = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        if (!placeholdersUsed.ContainsKey("APPLICANT_ADDRESS") || string.IsNullOrWhiteSpace(placeholdersUsed["APPLICANT_ADDRESS"]))
        {
            var appAddr = ResolveFieldFromCase(@case, "applicantAddress") ?? ResolveFieldFromCase(@case, "address");
            placeholdersUsed["APPLICANT_ADDRESS"] = !string.IsNullOrWhiteSpace(appAddr) ? appAddr : $"জেলা: {resolvedDistrict}";
        }

        if (!placeholdersUsed.ContainsKey("RESPONDENT_ADDRESS") || string.IsNullOrWhiteSpace(placeholdersUsed["RESPONDENT_ADDRESS"]))
        {
            var respAddr = ResolveFieldFromCase(@case, "respondentAddress") ?? ResolveFieldFromCase(@case, "employerAddress");
            placeholdersUsed["RESPONDENT_ADDRESS"] = !string.IsNullOrWhiteSpace(respAddr) ? respAddr : $"জেলা: {resolvedDistrict}";
        }

        // 5. Render the application body using edited draft text or the pre-approved template from DB
        var applicationBody = !string.IsNullOrWhiteSpace(@case.DraftComplaintText)
            ? ReplacePlaceholders(@case.DraftComplaintText, placeholdersUsed)
            : (template != null
                ? ReplacePlaceholders(template.BodyTemplateBn, placeholdersUsed)
                : "আইনি অভিযোগের পূর্ণাঙ্গ বিবরণ ও আবেদন।");

        var disclaimer = template?.DisclaimerNoticeBn
            ?? "সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র।";

        var applicantName = placeholdersUsed.GetValueOrDefault("APPLICANT_NAME")
            ?? ResolveFieldFromCase(@case, "applicant")
            ?? ResolveFieldFromCase(@case, "applicantName")
            ?? ResolveFieldFromCase(@case, "motherName")
            ?? ResolveFieldFromCase(@case, "workerName")
            ?? "[আবেদনকারী]";

        var respondentName = placeholdersUsed.GetValueOrDefault("RESPONDENT_NAME")
            ?? ResolveFieldFromCase(@case, "respondent")
            ?? ResolveFieldFromCase(@case, "respondentName")
            ?? ResolveFieldFromCase(@case, "husbandName")
            ?? ResolveFieldFromCase(@case, "employerName")
            ?? "[বিবাদী]";

        return new ApplicationPdfDocumentModel
        {
            CaseNumber = @case.CaseNumber,
            ParentCategoryCode = parentCatCode,
            ParentCategoryNameBn = parentCatNameBn,
            TemplateTitleBn = templateTitleBn,
            TemplateTitleEn = templateTitleEn,
            RecipientAuthorityBn = authority,
            SubjectBn = subject,
            ApplicantName = applicantName,
            RespondentName = respondentName,
            ApplicationBodyBn = applicationBody,
            DisclaimerNoticeBn = disclaimer,
            IsSafetyEscalated = @case.IsSafetyEscalated,
            SafetyReason = @case.SafetyReason,
            Status = @case.Status,
            ReviewStatus = "PENDING_REVIEW",
            CreatedAt = @case.CreatedAt,
            PlaceholdersUsed = placeholdersUsed
        };
    }

    private static string GetDefaultCategoryNameBn(string code) => code switch
    {
        "DOWER_MAINTENANCE" => "দেনমোহর ও খোরপোষ",
        "DOWRY_DOMESTIC_VIOLENCE" => "যৌতুক ও পারিবারিক সহিংসতা",
        "LABOUR_WAGE" => "শ্রমিক অধিকার ও বকেয়া মজুরি",
        "LAND_PROPERTY" => "জমি ও সম্পত্তি বিরোধ",
        "WOMEN_CHILD_PROTECTION" => "নারী ও শিশু সুরক্ষা",
        _ => "সাধারণ আইনি সহায়তা"
    };

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        try
        {
            // Index on application_templates
            var templateIndex = new CreateIndexModel<ApplicationTemplateModel>(
                Builders<ApplicationTemplateModel>.IndexKeys
                    .Ascending(x => x.CategoryCode)
                    .Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_application_templates_cat_active" });

            await _context.ApplicationTemplates.Indexes.CreateOneAsync(templateIndex, cancellationToken: ct);

            // Indexes on generated_applications
            var convIndex = new CreateIndexModel<GeneratedApplicationModel>(
                Builders<GeneratedApplicationModel>.IndexKeys.Ascending(x => x.ConversationId),
                new CreateIndexOptions { Name = "ix_generated_apps_conversation_id" });

            var categoryIndex = new CreateIndexModel<GeneratedApplicationModel>(
                Builders<GeneratedApplicationModel>.IndexKeys.Ascending(x => x.IncidentCategoryCode),
                new CreateIndexOptions { Name = "ix_generated_apps_category" });

            var statusIndex = new CreateIndexModel<GeneratedApplicationModel>(
                Builders<GeneratedApplicationModel>.IndexKeys.Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_generated_apps_status" });

            await _context.GeneratedApplications.Indexes.CreateManyAsync(
                [convIndex, categoryIndex, statusIndex],
                cancellationToken: ct);

            _logger.LogInformation("Application template indexes verified successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while ensuring application template indexes.");
        }
    }

    private static string ResolveCategoryCode(Conversation conversation)
    {
        var categoryCandidate = conversation.Category;
        if (string.IsNullOrWhiteSpace(categoryCandidate))
        {
            categoryCandidate = conversation.Analysis?.Category;
        }
        if (string.IsNullOrWhiteSpace(categoryCandidate))
        {
            categoryCandidate = conversation.IncidentType;
        }

        return ResolveCategoryCode(categoryCandidate);
    }

    private static string ResolveCategoryCode(string? categoryCandidate)
    {
        if (string.IsNullOrWhiteSpace(categoryCandidate))
        {
            return "OTHER";
        }

        var upper = categoryCandidate.Trim().ToUpperInvariant();

        if (upper is "DOWER_MAINTENANCE" or "DOWRY_DOMESTIC_VIOLENCE" or "LABOUR_WAGE" or "LAND_PROPERTY" or "WOMEN_CHILD_PROTECTION" or "OTHER")
        {
            return upper;
        }

        if (upper.Contains("DOWER") || upper.Contains("MAINTENANCE") || upper.Contains("দেনমোহর") || upper.Contains("ভরণপোষণ") || upper.Contains("খোরপোষ"))
            return "DOWER_MAINTENANCE";
        if (upper.Contains("DOWRY") || upper.Contains("DOMESTIC") || upper.Contains("যৌতুক") || upper.Contains("সহিংসতা") || upper.Contains("নির্যাতন"))
            return "DOWRY_DOMESTIC_VIOLENCE";
        if (upper.Contains("WAGE") || upper.Contains("LABOUR") || upper.Contains("LABOR") || upper.Contains("মজুরি") || upper.Contains("শ্রম") || upper.Contains("বেতন"))
            return "LABOUR_WAGE";
        if (upper.Contains("LAND") || upper.Contains("PROPERTY") || upper.Contains("জমি") || upper.Contains("সম্পত্তি") || upper.Contains("দখল"))
            return "LAND_PROPERTY";
        if (upper.Contains("WOMEN") || upper.Contains("CHILD") || upper.Contains("নারী") || upper.Contains("শিশু") || upper.Contains("কাস্টডি") || upper.Contains("অভিভাবকত্ব"))
            return "WOMEN_CHILD_PROTECTION";

        return "OTHER";
    }

    private static readonly Dictionary<string, string[]> PlaceholderToFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["APPLICANT_NAME"] = ["applicant", "applicantName", "motherName", "workerName", "victim", "clientName"],
        ["APPLICANT_ADDRESS"] = ["applicantAddress", "address", "presentAddress", "permanentAddress", "district"],
        ["RESPONDENT_NAME"] = ["respondent", "respondentName", "husbandName", "employerName", "opposingParty", "accused"],
        ["RESPONDENT_ADDRESS"] = ["respondentAddress", "employerAddress", "opposingAddress"],
        ["DISTRICT"] = ["district", "location"],
        ["MARRIAGE_DATE"] = ["marriageDate", "incidentDate", "date"],
        ["DOWER_TOTAL_AMOUNT"] = ["dowerAmount", "totalDower", "dowerTotal", "claimedAmount"],
        ["DOWER_PAID_AMOUNT"] = ["paidDowerAmount", "paidDower", "dowerPaid"],
        ["DOWER_UNPAID_AMOUNT"] = ["unpaidDowerAmount", "unpaidDower", "dowerUnpaid", "dowerAmount"],
        ["MAINTENANCE_DUE"] = ["maintenanceDue", "maintenanceAmount", "maintenanceClaim", "unpaidMaintenance"],
        ["DOWRY_DEMAND_AMOUNT"] = ["dowryDemandAmount", "dowryAmount", "claimedAmount"],
        ["ABUSE_TYPE"] = ["abuseType", "tortureType", "safetyAlert", "safetyReason"],
        ["INJURY_DETAILS"] = ["injuryDetails", "assaultDetails", "medicalDetails"],
        ["INCIDENT_LOCATION"] = ["incidentLocation", "location", "address", "district"],
        ["IMMEDIATE_THREAT"] = ["immediateThreat", "safetyRisk", "safetyAlert", "threatDetails", "safetyReason"],
        ["JOB_DESIGNATION"] = ["jobDesignation", "designation", "position", "occupation"],
        ["EMPLOYER_NAME"] = ["employerName", "companyName", "factoryName", "respondent"],
        ["MONTHLY_SALARY"] = ["monthlySalary", "salary", "wage"],
        ["UNPAID_WAGE_AMOUNT"] = ["unpaidWageAmount", "unpaidWage", "unpaidAmount", "claimedAmount"],
        ["UNPAID_MONTHS"] = ["unpaidMonths", "dueMonths", "monthsCount"],
        ["OVERTIME_DUE"] = ["overtimeDue", "overtimeAmount", "overtime"],
        ["PROPERTY_DESCRIPTION"] = ["propertyDescription", "landDescription", "schedule", "landDetails"],
        ["OWNERSHIP_TYPE"] = ["ownershipType", "titleType"],
        ["DISPUTE_NATURE"] = ["disputeNature", "disputeType", "conflictType"],
        ["FINANCIAL_LOSS"] = ["financialLoss", "lossAmount", "damageAmount"],
        ["CHILD_DETAILS"] = ["childDetails", "childrenInfo", "childCount"],
        ["DEPRIVATION_NATURE"] = ["deprivationNature", "movementRestriction", "custodyDisputeNature", "economicControl"],
        ["CHILD_SAFETY_RISK"] = ["childSafetyRisk", "safetyRisk", "immediateThreat"],
        ["CUSTODY_DISPUTE"] = ["custodyDispute", "custodyDisputeNature", "desiredRelief"],
        ["DESIRED_RELIEF"] = ["desiredRelief", "prayer", "relief"],
        ["INCIDENT_DETAILS"] = ["incidentDetails", "facts", "clientStatement"],
        ["EVIDENCE_LIST"] = ["evidenceList", "evidence", "documents"]
    };

    private static (Dictionary<string, string> Used, List<string> Missing) BuildPlaceholderDictionaryForCase(
        ApplicationTemplateModel template,
        CaseModel @case,
        IReadOnlyDictionary<string, string> answerDict,
        Dictionary<string, string>? explicitOverrides)
    {
        var used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        var extracted = @case.ExtractedData;
        var dialogueTranscripts = @case.DialogueHistory
            .Select(d => d.Transcript)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        var clientStatement = dialogueTranscripts.Count > 0 ? string.Join(" ", dialogueTranscripts) : string.Empty;

        foreach (var def in template.SupportedPlaceholders)
        {
            string? resolvedValue = null;
            PlaceholderToFieldAliases.TryGetValue(def.Key, out var canonicalAliases);

            // 1. Explicit overrides
            if (explicitOverrides != null && explicitOverrides.TryGetValue(def.Key, out var overrideVal) &&
                !string.IsNullOrWhiteSpace(overrideVal))
            {
                resolvedValue = overrideVal.Trim();
            }

            // 2. ExtractedData direct match
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                resolvedValue = ResolveFieldFromExtractedData(def.Key, extracted);
            }

            // 3. ExtractedData aliases match (from template def and canonical map)
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                foreach (var alias in def.Aliases)
                {
                    resolvedValue = ResolveFieldFromExtractedData(alias, extracted);
                    if (!string.IsNullOrWhiteSpace(resolvedValue)) break;
                }
            }
            if (string.IsNullOrWhiteSpace(resolvedValue) && canonicalAliases != null)
            {
                foreach (var alias in canonicalAliases)
                {
                    resolvedValue = ResolveFieldFromExtractedData(alias, extracted);
                    if (!string.IsNullOrWhiteSpace(resolvedValue)) break;
                }
            }

            // 4. Answer dictionary match
            if (string.IsNullOrWhiteSpace(resolvedValue) && answerDict.TryGetValue(def.Key, out var ansVal) &&
                !string.IsNullOrWhiteSpace(ansVal))
            {
                resolvedValue = ansVal.Trim();
            }

            // 5. Answer dictionary aliases
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                foreach (var alias in def.Aliases)
                {
                    if (answerDict.TryGetValue(alias, out var aliasVal) && !string.IsNullOrWhiteSpace(aliasVal))
                    {
                        resolvedValue = aliasVal.Trim();
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(resolvedValue) && canonicalAliases != null)
            {
                foreach (var alias in canonicalAliases)
                {
                    if (answerDict.TryGetValue(alias, out var aliasVal) && !string.IsNullOrWhiteSpace(aliasVal))
                    {
                        resolvedValue = aliasVal.Trim();
                        break;
                    }
                }
            }

            // 6. Dialogue history & fallback context
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                resolvedValue = ResolveFromCaseContext(def.Key, @case, clientStatement);
            }

            // 7. Store or fallback
            if (!string.IsNullOrWhiteSpace(resolvedValue) && !IsPlaceholderText(resolvedValue))
            {
                used[def.Key] = resolvedValue;
            }
            else
            {
                used[def.Key] = def.FallbackBn;
                if (def.IsRequired)
                {
                    missing.Add(def.Key);
                }
            }
        }

        // Add standard dates & disclaimer
        used["APPLICATION_DATE"] = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        used["CURRENT_DATE"] = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        used["DISCLAIMER_NOTICE"] = template.DisclaimerNoticeBn;

        return (used, missing);
    }

    private static string? ResolveFieldFromExtractedData(string key, Dictionary<string, ExtractedField> extracted)
    {
        if (extracted == null || extracted.Count == 0) return null;

        if (extracted.TryGetValue(key, out var field) && !string.IsNullOrWhiteSpace(field?.Value) && !IsPlaceholderText(field.Value))
        {
            return field.Value.Trim();
        }

        var lowerKey = key.ToLowerInvariant();
        var strippedKey = key.Replace("_", "").ToLowerInvariant();

        foreach (var kvp in extracted)
        {
            var kvpLower = kvp.Key.ToLowerInvariant();
            var kvpStripped = kvp.Key.Replace("_", "").ToLowerInvariant();
            if (kvpLower == lowerKey || kvpStripped == strippedKey)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value?.Value) && !IsPlaceholderText(kvp.Value.Value))
                {
                    return kvp.Value.Value.Trim();
                }
            }
        }

        // Check canonical aliases
        if (PlaceholderToFieldAliases.TryGetValue(key, out var aliases))
        {
            foreach (var alias in aliases)
            {
                if (extracted.TryGetValue(alias, out var af) && !string.IsNullOrWhiteSpace(af?.Value) && !IsPlaceholderText(af.Value))
                {
                    return af.Value.Trim();
                }

                var aliasStripped = alias.Replace("_", "").ToLowerInvariant();
                foreach (var kvp in extracted)
                {
                    var kvpStripped = kvp.Key.Replace("_", "").ToLowerInvariant();
                    if (kvpStripped == aliasStripped && !string.IsNullOrWhiteSpace(kvp.Value?.Value) && !IsPlaceholderText(kvp.Value.Value))
                    {
                        return kvp.Value.Value.Trim();
                    }
                }
            }
        }

        return null;
    }

    private static string? ResolveFieldFromCase(CaseModel @case, string fieldName)
    {
        if (@case.ExtractedData.TryGetValue(fieldName, out var field) && !string.IsNullOrWhiteSpace(field.Value) && !IsPlaceholderText(field.Value))
        {
            return field.Value.Trim();
        }

        var stripped = fieldName.Replace("_", "").ToLowerInvariant();
        foreach (var kvp in @case.ExtractedData)
        {
            var kvpStripped = kvp.Key.Replace("_", "").ToLowerInvariant();
            if (kvpStripped == stripped && !string.IsNullOrWhiteSpace(kvp.Value?.Value) && !IsPlaceholderText(kvp.Value.Value))
            {
                return kvp.Value.Value.Trim();
            }
        }

        return null;
    }

    private static string? ResolveFromCaseContext(string key, CaseModel @case, string clientStatement)
    {
        var upper = key.ToUpperInvariant();

        if (upper.Contains("APPLICANT_NAME") || upper.Contains("VICTIM"))
        {
            return ResolveFieldFromCase(@case, "applicant")
                ?? ResolveFieldFromCase(@case, "applicantName")
                ?? ResolveFieldFromCase(@case, "motherName")
                ?? ResolveFieldFromCase(@case, "workerName")
                ?? ResolveFieldFromCase(@case, "victim");
        }

        if (upper.Contains("APPLICANT_ADDRESS"))
        {
            return ResolveFieldFromCase(@case, "applicantAddress")
                ?? ResolveFieldFromCase(@case, "address")
                ?? ResolveFieldFromCase(@case, "district");
        }

        if (upper.Contains("RESPONDENT_NAME") || upper.Contains("ACCUSED") || upper.Contains("EMPLOYER"))
        {
            return ResolveFieldFromCase(@case, "respondent")
                ?? ResolveFieldFromCase(@case, "respondentName")
                ?? ResolveFieldFromCase(@case, "husbandName")
                ?? ResolveFieldFromCase(@case, "accused")
                ?? ResolveFieldFromCase(@case, "employerName");
        }

        if (upper.Contains("RESPONDENT_ADDRESS"))
        {
            return ResolveFieldFromCase(@case, "respondentAddress")
                ?? ResolveFieldFromCase(@case, "employerAddress")
                ?? ResolveFieldFromCase(@case, "address");
        }

        if (upper.Contains("DISTRICT") || upper.Contains("LOCATION"))
        {
            return ResolveFieldFromCase(@case, "district")
                ?? ResolveFieldFromCase(@case, "location")
                ?? "ঢাকা";
        }

        if (upper.Contains("INCIDENT_DATE") || upper.Contains("MARRIAGE_DATE") || upper.Contains("DATE"))
        {
            return ResolveFieldFromCase(@case, "marriageDate")
                ?? ResolveFieldFromCase(@case, "incidentDate")
                ?? ResolveFieldFromCase(@case, "date");
        }

        if (upper.Contains("DOWER_PAID") || upper.Contains("PAID_DOWER"))
        {
            return ResolveFieldFromCase(@case, "paidDowerAmount")
                ?? ResolveFieldFromCase(@case, "paidDower")
                ?? ResolveFieldFromCase(@case, "dowerPaid");
        }

        if (upper.Contains("DOWER_UNPAID") || upper.Contains("UNPAID_DOWER"))
        {
            return ResolveFieldFromCase(@case, "unpaidDowerAmount")
                ?? ResolveFieldFromCase(@case, "unpaidDower")
                ?? ResolveFieldFromCase(@case, "dowerUnpaid")
                ?? ResolveFieldFromCase(@case, "dowerAmount");
        }

        if (upper.Contains("DOWER_TOTAL") || upper.Contains("TOTAL_DOWER") || upper == "DOWER_AMOUNT")
        {
            return ResolveFieldFromCase(@case, "dowerAmount")
                ?? ResolveFieldFromCase(@case, "totalDower")
                ?? ResolveFieldFromCase(@case, "dowerTotal")
                ?? ResolveFieldFromCase(@case, "claimedAmount");
        }

        if (upper.Contains("MAINTENANCE"))
        {
            return ResolveFieldFromCase(@case, "maintenanceDue")
                ?? ResolveFieldFromCase(@case, "maintenanceAmount")
                ?? ResolveFieldFromCase(@case, "maintenanceClaim")
                ?? ResolveFieldFromCase(@case, "unpaidMaintenance");
        }

        if (upper.Contains("DOWRY"))
        {
            return ResolveFieldFromCase(@case, "dowryDemandAmount")
                ?? ResolveFieldFromCase(@case, "dowryAmount")
                ?? ResolveFieldFromCase(@case, "claimedAmount");
        }

        if (upper.Contains("ABUSE") || upper.Contains("TORTURE"))
        {
            return ResolveFieldFromCase(@case, "abuseType")
                ?? ResolveFieldFromCase(@case, "tortureType")
                ?? ResolveFieldFromCase(@case, "safetyAlert")
                ?? ResolveFieldFromCase(@case, "safetyReason");
        }

        if (upper.Contains("INJURY") || upper.Contains("ASSAULT"))
        {
            return ResolveFieldFromCase(@case, "injuryDetails")
                ?? ResolveFieldFromCase(@case, "assaultDetails")
                ?? ResolveFieldFromCase(@case, "medicalDetails");
        }

        if (upper.Contains("JOB_DESIGNATION") || upper.Contains("DESIGNATION"))
        {
            return ResolveFieldFromCase(@case, "jobDesignation")
                ?? ResolveFieldFromCase(@case, "designation")
                ?? ResolveFieldFromCase(@case, "position");
        }

        if (upper.Contains("MONTHLY_SALARY") || upper == "SALARY")
        {
            return ResolveFieldFromCase(@case, "monthlySalary")
                ?? ResolveFieldFromCase(@case, "salary")
                ?? ResolveFieldFromCase(@case, "wage");
        }

        if (upper.Contains("UNPAID_WAGE") || upper.Contains("WAGE_AMOUNT"))
        {
            return ResolveFieldFromCase(@case, "unpaidWageAmount")
                ?? ResolveFieldFromCase(@case, "unpaidWage")
                ?? ResolveFieldFromCase(@case, "unpaidAmount")
                ?? ResolveFieldFromCase(@case, "claimedAmount");
        }

        if (upper.Contains("UNPAID_MONTHS") || upper.Contains("MONTHS"))
        {
            return ResolveFieldFromCase(@case, "unpaidMonths")
                ?? ResolveFieldFromCase(@case, "dueMonths");
        }

        if (upper.Contains("OVERTIME"))
        {
            return ResolveFieldFromCase(@case, "overtimeDue")
                ?? ResolveFieldFromCase(@case, "overtimeAmount")
                ?? "০ (শূন্য)";
        }

        if (upper.Contains("PROPERTY") || upper.Contains("LAND"))
        {
            return ResolveFieldFromCase(@case, "propertyDescription")
                ?? ResolveFieldFromCase(@case, "landDescription")
                ?? ResolveFieldFromCase(@case, "landDetails");
        }

        if (upper.Contains("OWNERSHIP"))
        {
            return ResolveFieldFromCase(@case, "ownershipType")
                ?? ResolveFieldFromCase(@case, "titleType")
                ?? "পৈত্রিক / ক্রয়সূত্রে স্বত্বাধিকারী";
        }

        if (upper.Contains("DISPUTE_NATURE") || upper.Contains("DISPUTE"))
        {
            return ResolveFieldFromCase(@case, "disputeNature")
                ?? ResolveFieldFromCase(@case, "disputeType");
        }

        if (upper.Contains("FINANCIAL_LOSS") || upper.Contains("LOSS"))
        {
            return ResolveFieldFromCase(@case, "financialLoss")
                ?? ResolveFieldFromCase(@case, "lossAmount");
        }

        if (upper.Contains("CHILD_DETAILS") || upper.Contains("CHILDREN"))
        {
            return ResolveFieldFromCase(@case, "childDetails")
                ?? ResolveFieldFromCase(@case, "childrenInfo")
                ?? ResolveFieldFromCase(@case, "childCount");
        }

        if (upper.Contains("DEPRIVATION"))
        {
            return ResolveFieldFromCase(@case, "deprivationNature")
                ?? ResolveFieldFromCase(@case, "movementRestriction")
                ?? ResolveFieldFromCase(@case, "custodyDisputeNature");
        }

        if (upper.Contains("CHILD_SAFETY") || upper.Contains("SAFETY_RISK"))
        {
            return ResolveFieldFromCase(@case, "childSafetyRisk")
                ?? ResolveFieldFromCase(@case, "safetyRisk")
                ?? ResolveFieldFromCase(@case, "immediateThreat");
        }

        if (upper.Contains("CUSTODY"))
        {
            return ResolveFieldFromCase(@case, "custodyDisputeNature")
                ?? ResolveFieldFromCase(@case, "custodyDispute");
        }

        if (upper.Contains("DESIRED_RELIEF") || upper.Contains("RELIEF"))
        {
            return ResolveFieldFromCase(@case, "desiredRelief")
                ?? ResolveFieldFromCase(@case, "prayer");
        }

        if (upper.Contains("EVIDENCE"))
        {
            return ResolveFieldFromCase(@case, "evidenceList")
                ?? ResolveFieldFromCase(@case, "evidence")
                ?? ResolveFieldFromCase(@case, "documents");
        }

        if (upper.Contains("INCIDENT_DETAILS") || upper.Contains("STATEMENT"))
        {
            var details = ResolveFieldFromCase(@case, "incidentDetails")
                ?? ResolveFieldFromCase(@case, "facts")
                ?? ResolveFieldFromCase(@case, "clientStatement");
            if (!string.IsNullOrWhiteSpace(details)) return details;

            if (!string.IsNullOrWhiteSpace(clientStatement))
            {
                return $"আবেদনকারীর প্রাথমিক বিবরণ অনুযায়ী: \"{clientStatement}\"";
            }
        }

        return null;
    }
    private (Dictionary<string, string> Used, List<string> Missing) BuildPlaceholderDictionary(
        ApplicationTemplateModel template,
        Conversation conversation,
        GeneratedCaseSummaryModel? caseSummary,
        IReadOnlyDictionary<string, string> answerDict,
        Dictionary<string, string>? explicitOverrides)
    {
        var used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        var summaryFields = caseSummary?.StructuredContent ?? [];
        var extracted = conversation.Analysis?.ExtractedData;
        var facts = conversation.Analysis?.ImportantFactsMentioned ?? [];
        var clientStatement = ExtractClientStatement(conversation);

        foreach (var def in template.SupportedPlaceholders)
        {
            string? resolvedValue = null;

            // 1. Explicit overrides
            if (explicitOverrides != null && explicitOverrides.TryGetValue(def.Key, out var overrideVal) &&
                !string.IsNullOrWhiteSpace(overrideVal))
            {
                resolvedValue = overrideVal.Trim();
            }

            // 2. Check Generated Case Summary structured content fields
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                resolvedValue = ResolveFromCaseSummary(def, summaryFields);
            }

            // 3. Check direct answer dictionary
            if (string.IsNullOrWhiteSpace(resolvedValue) && answerDict.TryGetValue(def.Key, out var ansVal) &&
                !string.IsNullOrWhiteSpace(ansVal))
            {
                resolvedValue = ansVal.Trim();
            }

            // 4. Check aliases in answer dictionary
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                foreach (var alias in def.Aliases)
                {
                    if (answerDict.TryGetValue(alias, out var aliasVal) && !string.IsNullOrWhiteSpace(aliasVal))
                    {
                        resolvedValue = aliasVal.Trim();
                        break;
                    }
                }
            }

            // 5. Check conversation extracted facts & client statement
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                resolvedValue = ResolveFromConversationContext(def.Key, extracted, facts, clientStatement);
            }

            // 6. Safe substitution: Never invent facts.
            if (!string.IsNullOrWhiteSpace(resolvedValue) && !IsPlaceholderText(resolvedValue))
            {
                used[def.Key] = resolvedValue;
            }
            else
            {
                // Unavailable: mark fallback and record missing
                used[def.Key] = def.FallbackBn;
                if (def.IsRequired)
                {
                    missing.Add(def.Key);
                }
            }
        }

        // Add standard date and legal disclaimer
        used["APPLICATION_DATE"] = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        used["CURRENT_DATE"] = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        used["DISCLAIMER_NOTICE"] = template.DisclaimerNoticeBn;

        return (used, missing);
    }

    private static string? ResolveFromCaseSummary(
        ApplicationPlaceholderDefinition def,
        List<GeneratedCaseSummaryField> summaryFields)
    {
        if (summaryFields.Count == 0) return null;

        // 1. Exact match on Key
        var match = summaryFields.FirstOrDefault(f =>
            f.Key.Equals(def.Key, StringComparison.OrdinalIgnoreCase) &&
            f.Status != "Missing" &&
            !string.IsNullOrWhiteSpace(f.Value) &&
            !IsPlaceholderText(f.Value));

        if (match != null) return match.Value.Trim();

        // 2. Case-insensitive / stripped key match
        var strippedDef = def.Key.Replace("_", "").ToLowerInvariant();
        match = summaryFields.FirstOrDefault(f =>
            f.Key.Replace("_", "").ToLowerInvariant() == strippedDef &&
            f.Status != "Missing" &&
            !string.IsNullOrWhiteSpace(f.Value) &&
            !IsPlaceholderText(f.Value));

        if (match != null) return match.Value.Trim();

        // 3. Template defined Aliases
        foreach (var alias in def.Aliases)
        {
            var aliasMatch = summaryFields.FirstOrDefault(f =>
                f.Key.Equals(alias, StringComparison.OrdinalIgnoreCase) &&
                f.Status != "Missing" &&
                !string.IsNullOrWhiteSpace(f.Value) &&
                !IsPlaceholderText(f.Value));

            if (aliasMatch != null) return aliasMatch.Value.Trim();
        }

        // 4. Canonical field aliases from dictionary
        if (PlaceholderToFieldAliases.TryGetValue(def.Key, out var canonicalList))
        {
            foreach (var alias in canonicalList)
            {
                var strippedAlias = alias.Replace("_", "").ToLowerInvariant();
                var aliasMatch = summaryFields.FirstOrDefault(f =>
                    (f.Key.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                     f.Key.Replace("_", "").ToLowerInvariant() == strippedAlias) &&
                    f.Status != "Missing" &&
                    !string.IsNullOrWhiteSpace(f.Value) &&
                    !IsPlaceholderText(f.Value));

                if (aliasMatch != null) return aliasMatch.Value.Trim();
            }
        }

        return null;
    }

    private static string? ResolveFromConversationContext(
        string key,
        ExtractedLegalData? extracted,
        List<string> facts,
        string clientStatement)
    {
        var upper = key.ToUpperInvariant();

        if (upper.Contains("APPLICANT_NAME") || upper.Contains("VICTIM"))
        {
            if (!string.IsNullOrWhiteSpace(extracted?.Applicant?.Value))
                return extracted.Applicant.Value;
        }

        if (upper.Contains("RESPONDENT_NAME") || upper.Contains("ACCUSED") || upper.Contains("EMPLOYER"))
        {
            if (!string.IsNullOrWhiteSpace(extracted?.Respondent?.Value))
                return extracted.Respondent.Value;
        }

        if (upper.Contains("INCIDENT_DATE") || upper.Contains("DATE"))
        {
            if (!string.IsNullOrWhiteSpace(extracted?.MarriageOrIncidentDate?.Value))
                return extracted.MarriageOrIncidentDate.Value;
        }

        if (upper.Contains("AMOUNT") || upper.Contains("WAGE") || upper.Contains("DOWER") || upper.Contains("DOWRY"))
        {
            if (!string.IsNullOrWhiteSpace(extracted?.DisputedAmount?.Value))
                return extracted.DisputedAmount.Value;
        }

        if (upper.Contains("INCIDENT_DETAILS") || upper.Contains("STATEMENT"))
        {
            if (!string.IsNullOrWhiteSpace(clientStatement))
            {
                // Qualify unverified client statements responsibly
                return $"আবেদনকারীর প্রাথমিক মৌখিক ও লিখিত বিবরণ অনুযায়ী: \"{clientStatement}\"";
            }

            if (facts.Count > 0)
            {
                return $"আবেদনকারীর প্রদত্ত বিবরণ অনুযায়ী: {string.Join("। ", facts)}";
            }
        }

        if (upper.Contains("CLIENT_STATEMENT"))
        {
            return !string.IsNullOrWhiteSpace(clientStatement)
                ? $"\"{clientStatement}\""
                : "[আবেদনকারীর সরাসরি বক্তব্য এখনো লিপিবদ্ধ হয়নি]";
        }

        return null;
    }

    private static string ExtractClientStatement(Conversation conversation)
    {
        if (conversation.Messages != null && conversation.Messages.Count > 0)
        {
            var userUtterances = conversation.Messages
                .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                            m.Role.Equals("client", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Text?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (userUtterances.Count > 0)
            {
                return string.Join(" ", userUtterances);
            }
        }

        if (conversation.Analysis?.ImportantFactsMentioned != null && conversation.Analysis.ImportantFactsMentioned.Count > 0)
        {
            return string.Join("। ", conversation.Analysis.ImportantFactsMentioned);
        }

        return string.Empty;
    }

    private static bool IsPlaceholderText(string val) =>
        val.Contains("তথ্য পাওয়া যায়নি", StringComparison.OrdinalIgnoreCase) ||
        val.Contains("পূরণীয়", StringComparison.OrdinalIgnoreCase);

    private static string ReplacePlaceholders(string templateText, Dictionary<string, string> placeholders)
    {
        if (string.IsNullOrWhiteSpace(templateText)) return string.Empty;

        return PlaceholderRegex().Replace(templateText, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (placeholders.TryGetValue(key, out var val))
            {
                return val;
            }

            return $"[{key} - তথ্য পাওয়া যায়নি / পূরণীয়]";
        });
    }

    [GeneratedRegex(@"\{\{([A-Z0-9_]+)\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();
}
