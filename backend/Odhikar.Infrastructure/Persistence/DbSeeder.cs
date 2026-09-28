using MongoDB.Driver;
using Odhikar.Domain.Models;

namespace Odhikar.Infrastructure.Persistence;

/// <summary>
/// Idempotent seed of fabricated legal-aid clinics and a couple of demo cases so
/// the paralegal dashboard has data on first run. Safe to call on every startup.
/// </summary>
public sealed class DbSeeder(MongoDbContext context, ILogger<DbSeeder> logger)
{
    private readonly MongoDbContext _context = context;
    private readonly ILogger<DbSeeder> _logger = logger;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedClinicsAsync(ct);
        await SeedDemoCasesAsync(ct);
        await SeedParentCategoriesAsync(ct);
        await SeedIncidentTypesAsync(ct);
        await EnsureIncidentTypeParentMappingsAsync(ct);
        await SeedCaseSummaryTemplatesAsync(ct);
        await SeedQuestionBankAsync(ct);
        await SeedApplicationTemplatesAsync(ct);
    }

    private async Task SeedClinicsAsync(CancellationToken ct)
    {
        var existing = await _context.Clinics.CountDocumentsAsync(FilterDefinition<ClinicModel>.Empty, cancellationToken: ct);
        if (existing > 0)
        {
            return;
        }

        var baseDay = DateTime.UtcNow.Date.AddDays(1).AddHours(10); // tomorrow 10:00
        DateTime[] Slots(int startOffsetDays) =>
        [
            baseDay.AddDays(startOffsetDays),
            baseDay.AddDays(startOffsetDays).AddHours(2),
            baseDay.AddDays(startOffsetDays + 2),
            baseDay.AddDays(startOffsetDays + 2).AddHours(3)
        ];

        var clinics = new List<ClinicModel>
        {
            new()
            {
                Name = "ঢাকা আইনি সহায়তা কেন্দ্র",
                District = "Dhaka",
                Address = "১২/এ, সেগুনবাগিচা, ঢাকা-১০০০",
                AvailableSlots = Slots(0).ToList()
            },
            new()
            {
                Name = "চট্টগ্রাম লিগ্যাল এইড ক্লিনিক",
                District = "Chattogram",
                Address = "৪৫, জামালখান রোড, চট্টগ্রাম-৪০০০",
                AvailableSlots = Slots(1).ToList()
            },
            new()
            {
                Name = "রাজশাহী নারী অধিকার সেল",
                District = "Rajshahi",
                Address = "৭, গ্রেটার রোড, রাজশাহী-৬০০০",
                AvailableSlots = Slots(2).ToList()
            },
            new()
            {
                Name = "খুলনা শ্রমিক আইন সহায়তা",
                District = "Khulna",
                Address = "২৩, কেডিএ এভিনিউ, খুলনা-৯১০০",
                AvailableSlots = Slots(3).ToList()
            }
        };

        await _context.Clinics.InsertManyAsync(clinics, cancellationToken: ct);
        _logger.LogInformation("Seeded {Count} clinics.", clinics.Count);
    }

    private async Task SeedDemoCasesAsync(CancellationToken ct)
    {
        var existing = await _context.Cases.CountDocumentsAsync(FilterDefinition<CaseModel>.Empty, cancellationToken: ct);
        if (existing > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var urgent = new CaseModel
        {
            CaseNumber = "ODH-2026-0417",
            Category = LegalCategories.DowerMaintenance,
            CategoryConfidence = 0.92,
            IsSafetyEscalated = true,
            SafetyReason = "ক্লায়েন্ট শারীরিক নির্যাতনের কথা উল্লেখ করেছেন (\"মারধর\").",
            Status = CaseStatuses.Urgent,
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2),
            ExtractedData = new Dictionary<string, ExtractedField>
            {
                ["applicant"] = ExtractedField.Stated("রহিমা বেগম"),
                ["respondent"] = ExtractedField.Stated("করিম মিয়া"),
                ["marriageDate"] = ExtractedField.Stated("2019-03-12"),
                ["dowerAmount"] = ExtractedField.Stated("300000"),
                ["claimedAmount"] = ExtractedField.Inferred("300000", "দেনমোহরের সম্পূর্ণ পরিমাণ দাবি হিসেবে ধরা হয়েছে।"),
                ["evidenceList"] = ExtractedField.Stated("কাবিননামা, সাক্ষী দুইজন")
            },
            DialogueHistory =
            [
                new DialogueTurn
                {
                    TurnIndex = 0,
                    Transcript = "আমার স্বামী দেনমোহর দেয় নাই, আর আমাকে মারধর করে।",
                    SystemPrompt = "আপনি নিরাপদ আছেন তো? জরুরি সহায়তার জন্য ১৬৪৩০ নম্বরে কল করুন।",
                    SafetyTriggered = true,
                    CreatedAt = now.AddHours(-2)
                }
            ]
        };

        var normal = new CaseModel
        {
            CaseNumber = "ODH-2026-0418",
            Category = LegalCategories.UnpaidWages,
            CategoryConfidence = 0.81,
            Status = CaseStatuses.New,
            CreatedAt = now.AddHours(-1),
            UpdatedAt = now.AddHours(-1),
            ExtractedData = new Dictionary<string, ExtractedField>
            {
                ["applicant"] = ExtractedField.Stated("জসিম উদ্দিন"),
                ["respondent"] = ExtractedField.Stated("রূপসী গার্মেন্টস লিমিটেড"),
                ["claimedAmount"] = ExtractedField.Inferred("18000", "মোট প্রাপ্য ৪২,০০০ − পরিশোধিত ২৪,০০০ = ১৮,০০০।"),
                ["evidenceList"] = ExtractedField.Inferred(null, "ক্লায়েন্ট কোনো প্রমাণ উল্লেখ করেননি।")
            },
            DialogueHistory =
            [
                new DialogueTurn
                {
                    TurnIndex = 0,
                    Transcript = "আমি তিন মাস কাজ করেছি, কিন্তু পুরো বেতন পাইনি।",
                    SystemPrompt = "আপনার মোট প্রাপ্য বেতন কত ছিল এবং এ পর্যন্ত কত টাকা পেয়েছেন?",
                    CreatedAt = now.AddHours(-1)
                }
            ]
        };

        await _context.Cases.InsertManyAsync([urgent, normal], cancellationToken: ct);
        _logger.LogInformation("Seeded demo cases.");
    }

    private async Task SeedIncidentTypesAsync(CancellationToken ct)
    {
        var existing = await _context.IncidentTypes.CountDocumentsAsync(FilterDefinition<IncidentTypeModel>.Empty, cancellationToken: ct);
        if (existing > 0)
        {
            return;
        }

        var incidentTypes = new List<IncidentTypeModel>
        {
            new() { Code = "DOWRY_DEMAND", Name = "যৌতুক দাবি", NameEn = "Dowry Demand", Category = LegalCategories.Dowry },
            new() { Code = "DOWRY_PAYMENT", Name = "যৌতুক প্রদান", NameEn = "Dowry Payment", Category = LegalCategories.Dowry },
            new() { Code = "DOWER_NONPAYMENT", Name = "দেনমোহর না দেওয়া", NameEn = "Non-payment of Dower", Category = LegalCategories.DowerMaintenance },
            new() { Code = "MAINTENANCE_NONPAYMENT", Name = "ভরণপোষণ না দেওয়া", NameEn = "Non-payment of Maintenance", Category = LegalCategories.DowerMaintenance },
            new() { Code = "CHILD_MAINTENANCE_NONPAYMENT", Name = "সন্তানের ভরণপোষণ না দেওয়া", NameEn = "Non-payment of Child Maintenance", Category = LegalCategories.DowerMaintenance },
            new() { Code = "HUSBAND_EVICTION_ATTEMPT", Name = "স্বামী কর্তৃক স্ত্রীকে বাড়ি থেকে বের করে দেওয়ার চেষ্টা", NameEn = "Attempted Eviction of Wife by Husband", Category = LegalCategories.DowerMaintenance },
            new() { Code = "DOMESTIC_PHYSICAL_ABUSE", Name = "পারিবারিক শারীরিক নির্যাতন", NameEn = "Domestic Physical Abuse", Category = LegalCategories.DowerMaintenance },
            new() { Code = "DEATH_THREAT", Name = "হত্যার হুমকি", NameEn = "Death Threat", Category = LegalCategories.DowerMaintenance },
            new() { Code = "PHYSICAL_HARM_THREAT", Name = "শারীরিক ক্ষতির হুমকি", NameEn = "Threat of Physical Harm", Category = LegalCategories.DowerMaintenance },
            new() { Code = "MEDICAL_OBSTRUCTION", Name = "চিকিৎসা নিতে বাধা দেওয়া", NameEn = "Obstruction to Medical Treatment", Category = LegalCategories.DowerMaintenance },
            new() { Code = "FORCED_CONFINEMENT", Name = "জোরপূর্বক আটকে রাখা", NameEn = "Forced Confinement", Category = LegalCategories.DowerMaintenance },
            new() { Code = "MOVEMENT_RESTRICTION", Name = "চলাফেরায় বাধা দেওয়া", NameEn = "Restriction of Movement", Category = LegalCategories.DowerMaintenance },
            new() { Code = "ECONOMIC_CONTROL", Name = "অর্থনৈতিক নিয়ন্ত্রণ", NameEn = "Economic Control", Category = LegalCategories.DowerMaintenance },
            new() { Code = "PROPERTY_DEPRIVATION", Name = "সম্পত্তি থেকে বঞ্চিত করা", NameEn = "Deprivation of Property", Category = LegalCategories.LandDispute },
            new() { Code = "WOMEN_INHERITANCE_DENIAL", Name = "উত্তরাধিকার সম্পত্তিতে নারীর অংশ না দেওয়া", NameEn = "Denial of Woman's Share in Inheritance Property", Category = LegalCategories.LandDispute },
            new() { Code = "WOMEN_PROPERTY_RIGHT_DISPUTE", Name = "স্বামীর/পরিবারের সম্পত্তি নিয়ে নারীর অধিকারসংক্রান্ত বিরোধ", NameEn = "Dispute Regarding Woman's Rights Over Husband's/Family Property", Category = LegalCategories.LandDispute },
            new() { Code = "FORCED_PROPERTY_TRANSFER_PRESSURE", Name = "জোরপূর্বক সম্পত্তি হস্তান্তরের চাপ", NameEn = "Pressure for Forced Property Transfer", Category = LegalCategories.LandDispute },
            new() { Code = "MARRIAGE_FRAUD", Name = "বিবাহ-সংক্রান্ত প্রতারণা", NameEn = "Marriage Fraud", Category = LegalCategories.DowerMaintenance },
            new() { Code = "POST_DIVORCE_DUES_NONPAYMENT", Name = "বিবাহবিচ্ছেদের পর নারীর পাওনা না দেওয়া", NameEn = "Non-payment of Woman's Dues Post-Divorce", Category = LegalCategories.DowerMaintenance },
            new() { Code = "CHILD_CUSTODY_DISPUTE", Name = "সন্তানের হেফাজত নিয়ে বিরোধ", NameEn = "Child Custody Dispute", Category = LegalCategories.DowerMaintenance },
            new() { Code = "CHILD_VISITATION_OBSTRUCTION", Name = "সন্তানের সাথে দেখা করতে বাধা দেওয়া", NameEn = "Obstruction to Child Visitation", Category = LegalCategories.DowerMaintenance }
        };

        await _context.IncidentTypes.InsertManyAsync(incidentTypes, cancellationToken: ct);
        _logger.LogInformation("Seeded {Count} incident types.", incidentTypes.Count);
    }


    private async Task SeedQuestionBankAsync(CancellationToken ct)
    {
        // 1. Ensure indexes on question_bank
        try
        {
            var idKeys = Builders<QuestionBankItem>.IndexKeys.Ascending(x => x.QuestionId);
            await _context.QuestionBank.Indexes.CreateOneAsync(
                new CreateIndexModel<QuestionBankItem>(idKeys, new CreateIndexOptions { Unique = true, Name = "ux_question_bank_question_id" }),
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index on question_bank.questionId checked/handled.");
        }

        try
        {
            var parentKeys = Builders<QuestionBankItem>.IndexKeys.Ascending(x => x.ParentCategoryCodes);
            await _context.QuestionBank.Indexes.CreateOneAsync(
                new CreateIndexModel<QuestionBankItem>(parentKeys, new CreateIndexOptions { Name = "ix_question_bank_parent_category_codes" }),
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index on question_bank.parentCategoryCodes checked/handled.");
        }

        // Clean and reset existing question bank collection so only pure curated template questions exist
        await _context.QuestionBank.DeleteManyAsync(FilterDefinition<QuestionBankItem>.Empty, ct);

        var items = new List<QuestionBankItem>
        {
            // =========================================================================
            // ১. দেনমোহর ও ভরণপোষণ সারসংক্ষেপ টেমপ্লেট (DOWER_MAINTENANCE)
            // =========================================================================
            new()
            {
                QuestionId = "Q-DOWER-001",
                Category = "applicant",
                QuestionBangla = "আপনার পূর্ণ নাম কী?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT", "MAINTENANCE_NONPAYMENT", "POST_DIVORCE_DUES_NONPAYMENT"],
                Priority = 10,
                IsActive = true,
                RequiredFactKey = "applicant",
                ContextKeywords = ["নাম", "স্ত্রী", "আবেদনকারী"],
                Notes = "Applicant full name."
            },
            new()
            {
                QuestionId = "Q-DOWER-002",
                Category = "perpetrator",
                QuestionBangla = "আপনার স্বামীর পূর্ণ নাম কী?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT", "MAINTENANCE_NONPAYMENT"],
                Priority = 20,
                IsActive = true,
                RequiredFactKey = "respondent",
                ContextKeywords = ["স্বামী", "নাম", "ঠিকানা"],
                Notes = "Respondent husband's full name."
            },
            new()
            {
                QuestionId = "Q-DOWER-003",
                Category = "incident_time",
                QuestionBangla = "আপনাদের বিবাহের সঠিক তারিখ বা সাল কোনটি?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT"],
                Priority = 30,
                IsActive = true,
                RequiredFactKey = "marriageDate",
                ContextKeywords = ["বিবাহ", "বিয়ে", "তারিখ", "সাল"],
                Notes = "Marriage date or year."
            },
            new()
            {
                QuestionId = "Q-DOWER-004",
                Category = "financial_details",
                QuestionBangla = "কাবিননামা অনুযায়ী আপনার দেনমোহরের মোট ধার্যকৃত অঙ্ক কত টাকা?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT"],
                Priority = 40,
                IsActive = true,
                RequiredFactKey = "dowerAmount",
                ContextKeywords = ["দেনমোহর", "কাবিননামা", "টাকা", "ধার্য"],
                Notes = "Total fixed dower amount as per Nikahnama."
            },
            new()
            {
                QuestionId = "Q-DOWER-005",
                Category = "financial_details",
                QuestionBangla = "বিবাহের সময় বা পরবর্তীতে আপনাকে কত টাকা দেনমোহর বাবদ পরিশোধ করা হয়েছে?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT"],
                Priority = 50,
                IsActive = true,
                RequiredFactKey = "paidDowerAmount",
                ContextKeywords = ["উসুল", "পরিশোধ", "দেনমোহর", "টাকা"],
                Notes = "Amount of dower already paid/satisfied."
            },
            new()
            {
                QuestionId = "Q-DOWER-006",
                Category = "financial_details",
                QuestionBangla = "বর্তমানে আপনার বকেয়া দেনমোহরের মোট পরিমাণ কত টাকা?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT", "POST_DIVORCE_DUES_NONPAYMENT"],
                Priority = 60,
                IsActive = true,
                RequiredFactKey = "unpaidDowerAmount",
                ContextKeywords = ["বকেয়া", "দেনমোহর", "দাবি", "টাকা"],
                Notes = "Current unpaid dower dues."
            },
            new()
            {
                QuestionId = "Q-DOWER-007",
                Category = "financial_details",
                QuestionBangla = "স্বামী খোরপোষ না দেওয়ার কারণে আপনার কত টাকা বকেয়া ভরণপোষণ পাওনা রয়েছে?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "MAINTENANCE_NONPAYMENT"],
                Priority = 70,
                IsActive = true,
                RequiredFactKey = "maintenanceDue",
                ContextKeywords = ["খোরপোষ", "ভরণপোষণ", "বকেয়া", "টাকা"],
                Notes = "Past and due maintenance claim."
            },
            new()
            {
                QuestionId = "Q-DOWER-008",
                Category = "financial_details",
                QuestionBangla = "আপনার সন্তানদের সংখ্যা কত এবং তাদের জন্য প্রতি মাসে কত টাকা ভরণপোষণ দাবি করছেন?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "CHILD_MAINTENANCE_NONPAYMENT"],
                Priority = 80,
                IsActive = true,
                RequiredFactKey = "childCount",
                ContextKeywords = ["সন্তান", "বাচ্চা", "সংখ্যা", "ভরণপোষণ", "মাসিক"],
                Notes = "Number of children and requested monthly child maintenance."
            },
            new()
            {
                QuestionId = "Q-DOWER-009",
                Category = "evidence_documents",
                QuestionBangla = "আপনার নিকট কী কী প্রমাণাদি বা নথিপত্র (যেমন: কাবিননামা, শালিসনামা) সংরক্ষিত আছে?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE", "DOWER_NONPAYMENT"],
                Priority = 90,
                IsActive = true,
                RequiredFactKey = "evidenceList",
                ContextKeywords = ["কাবিননামা", "শালিসনামা", "প্রমাণ", "নথিপত্র", "কাগজ"],
                Notes = "Available documentary evidence."
            },
            new()
            {
                QuestionId = "Q-DOWER-010",
                Category = "desired_relief",
                QuestionBangla = "এই বিষয়ে আপনি কী ধরনের আইনি প্রতিকার (যেমন: পারিবারিক আদালতে মামলা বা আপস) চাচ্ছেন?",
                ParentCategoryCodes = ["DOWER_MAINTENANCE"],
                RelatedIncidentTypes = ["DOWER_MAINTENANCE"],
                Priority = 100,
                IsActive = true,
                RequiredFactKey = "desiredRelief",
                ContextKeywords = ["মামলা", "আদালত", "আপস", "প্রতিকার"],
                Notes = "Desired legal remedy or relief."
            },

            // =========================================================================
            // ২. যৌতুক ও পারিবারিক সহিংসতা সারসংক্ষেপ টেমপ্লেট (DOWRY_DOMESTIC_VIOLENCE)
            // =========================================================================
            new()
            {
                QuestionId = "Q-DOWRY-001",
                Category = "applicant",
                QuestionBangla = "নির্যাতনের শিকার ব্যক্তি বা আবেদনকারীর পূর্ণ নাম কী?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DOWRY", "DOMESTIC_PHYSICAL_ABUSE"],
                Priority = 10,
                IsActive = true,
                RequiredFactKey = "applicant",
                ContextKeywords = ["নাম", "আবেদনকারী", "নির্যাতিত"],
                Notes = "Victim / applicant full name."
            },
            new()
            {
                QuestionId = "Q-DOWRY-002",
                Category = "perpetrator",
                QuestionBangla = "অভিযুক্ত ব্যক্তি বা নির্যাতনকারীর পূর্ণ নাম কী?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DOWRY", "DOMESTIC_PHYSICAL_ABUSE"],
                Priority = 20,
                IsActive = true,
                RequiredFactKey = "respondent",
                ContextKeywords = ["অভিযুক্ত", "নির্যাতনকারী", "নাম", "স্বামী"],
                Notes = "Accused / abuser full name."
            },
            new()
            {
                QuestionId = "Q-DOWRY-003",
                Category = "financial_details",
                QuestionBangla = "যৌতুক হিসেবে সুনির্দিষ্টভাবে কত টাকা বা কী কী মালামাল দাবি করা হয়েছে?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DOWRY", "DOWRY_DEMAND"],
                Priority = 30,
                IsActive = true,
                RequiredFactKey = "dowryDemandAmount",
                ContextKeywords = ["যৌতুক", "দাবি", "টাকা", "মালামাল", "স্বর্ণ", "বাইক"],
                Notes = "Details and amount of dowry demanded."
            },
            new()
            {
                QuestionId = "Q-DOWRY-004",
                Category = "financial_details",
                QuestionBangla = "যৌতুক বাবদ পূর্বে ইতোমধ্যে কত টাকা বা মালামাল প্রদান করা হয়েছিল?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DOWRY", "DOWRY_PAYMENT"],
                Priority = 40,
                IsActive = true,
                RequiredFactKey = "dowryPaidAmount",
                ContextKeywords = ["যৌতুক", "প্রদান", "দিয়েছি", "টাকা"],
                Notes = "Amount or goods previously given as dowry."
            },
            new()
            {
                QuestionId = "Q-DOWRY-005",
                Category = "threat_details",
                QuestionBangla = "আপনার ওপর মূলত কী ধরনের শারীরিক বা মানসিক নির্যাতন চালানো হয়েছে?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DOMESTIC_PHYSICAL_ABUSE", "PHYSICAL_HARM_THREAT"],
                Priority = 50,
                IsActive = true,
                RequiredFactKey = "abuseType",
                ContextKeywords = ["নির্যাতন", "শারীরিক", "মানসিক", "মারধর"],
                Notes = "Nature and method of physical/mental abuse."
            },
            new()
            {
                QuestionId = "Q-DOWRY-006",
                Category = "injury_details",
                QuestionBangla = "শারীরিক আঘাতের বিবরণ এবং হাসপাতালে নেওয়া কোনো চিকিৎসার তথ্য উল্লেখ করুন।",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DOMESTIC_PHYSICAL_ABUSE"],
                Priority = 60,
                IsActive = true,
                RequiredFactKey = "injuryDetails",
                ContextKeywords = ["আঘাত", "জখম", "হাসপাতাল", "চিকিৎসা", "ডাক্তার"],
                Notes = "Physical injuries sustained and medical care received."
            },
            new()
            {
                QuestionId = "Q-DOWRY-007",
                Category = "incident_location",
                QuestionBangla = "নির্যাতন বা বাড়ি থেকে তাড়িয়ে দেওয়ার ঘটনাটি সুনির্দিষ্টভাবে কোথায় ঘটেছে?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "HUSBAND_EVICTION_ATTEMPT"],
                Priority = 70,
                IsActive = true,
                RequiredFactKey = "incidentLocation",
                ContextKeywords = ["ঘটনাস্থল", "বাড়ি", "স্থান", "ঠিকানা", "তাড়িয়ে"],
                Notes = "Location of the domestic abuse or eviction incident."
            },
            new()
            {
                QuestionId = "Q-DOWRY-008",
                Category = "current_safety",
                QuestionBangla = "বর্তমানে আপনার ওপর কোনো জরুরি শারীরিক ক্ষতি বা জীবননাশের হুমকি আছে কি?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE", "DEATH_THREAT", "PHYSICAL_HARM_THREAT"],
                Priority = 80,
                IsActive = true,
                RequiredFactKey = "immediateThreat",
                ContextKeywords = ["হুমকি", "জীবননাশ", "জরুরি", "ঝুঁকি", "ভয়"],
                Notes = "Immediate safety risk or death threat evaluation."
            },
            new()
            {
                QuestionId = "Q-DOWRY-009",
                Category = "evidence_documents",
                QuestionBangla = "এই বিষয়ে স্থানীয় থানায় কোনো জিডি (GD) বা লিখিত অভিযোগ দায়ের করা হয়েছে কি?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE"],
                Priority = 90,
                IsActive = true,
                RequiredFactKey = "policeReport",
                ContextKeywords = ["জিডি", "থানা", "পুলিশ", "অভিযোগ", "GD"],
                Notes = "Police general diary (GD) or FIR status."
            },
            new()
            {
                QuestionId = "Q-DOWRY-010",
                Category = "witness_details",
                QuestionBangla = "ঘটনার কোনো প্রত্যক্ষদর্শী সাক্ষী বা অন্য কোনো প্রমাণাদি আছে কি?",
                ParentCategoryCodes = ["DOWRY_DOMESTIC_VIOLENCE"],
                RelatedIncidentTypes = ["DOWRY_DOMESTIC_VIOLENCE"],
                Priority = 100,
                IsActive = true,
                RequiredFactKey = "witnessList",
                ContextKeywords = ["সাক্ষী", "প্রত্যক্ষদর্শী", "প্রমাণ"],
                Notes = "Eyewitnesses and corroborating evidence."
            },

            // =========================================================================
            // ৩. বকেয়া মজুরি ও শ্রম বিরোধ সারসংক্ষেপ টেমপ্লেট (LABOUR_WAGE)
            // =========================================================================
            new()
            {
                QuestionId = "Q-WAGE-001",
                Category = "applicant",
                QuestionBangla = "আপনার (শ্রমিক/কর্মচারী) পূর্ণ নাম কী?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 10,
                IsActive = true,
                RequiredFactKey = "applicant",
                ContextKeywords = ["নাম", "শ্রমিক", "কর্মচারী"],
                Notes = "Worker / employee full name."
            },
            new()
            {
                QuestionId = "Q-WAGE-002",
                Category = "perpetrator",
                QuestionBangla = "আপনার মালিক বা প্রতিষ্ঠানের নাম এবং প্রতিষ্ঠানের পূর্ণ ঠিকানা কী?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 20,
                IsActive = true,
                RequiredFactKey = "employerName",
                ContextKeywords = ["মালিক", "প্রতিষ্ঠান", "কোম্পানি", "কারখানা", "ঠিকানা"],
                Notes = "Employer / company name and registered venue."
            },
            new()
            {
                QuestionId = "Q-WAGE-003",
                Category = "incident_description",
                QuestionBangla = "উক্ত প্রতিষ্ঠানে আপনার পদবী (Designation) বা কাজের বিবরণ কী ছিল?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 30,
                IsActive = true,
                RequiredFactKey = "jobDesignation",
                ContextKeywords = ["পদবী", "কাজের বিবরণ", "Designation", "কাজ"],
                Notes = "Job designation and role responsibilities."
            },
            new()
            {
                QuestionId = "Q-WAGE-004",
                Category = "financial_details",
                QuestionBangla = "আপনার মাসিক মূল মজুরি বা দৈনিক হাজিরা হার কত ছিল?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 40,
                IsActive = true,
                RequiredFactKey = "monthlySalary",
                ContextKeywords = ["মজুরি", "বেতন", "মাসিক", "দৈনিক", "হার", "টাকা"],
                Notes = "Monthly wage or daily wage rate."
            },
            new()
            {
                QuestionId = "Q-WAGE-005",
                Category = "incident_time",
                QuestionBangla = "মোট কত মাস বা কতদিনের বেতন বকেয়া রয়েছে?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 50,
                IsActive = true,
                RequiredFactKey = "unpaidMonths",
                ContextKeywords = ["বকেয়া", "মাস", "দিন", "সময়কাল"],
                Notes = "Number of unpaid wage months/days."
            },
            new()
            {
                QuestionId = "Q-WAGE-006",
                Category = "financial_details",
                QuestionBangla = "প্রতিষ্ঠানটির নিকট আপনার মোট বকেয়া মজুরির দাবি কত টাকা?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 60,
                IsActive = true,
                RequiredFactKey = "unpaidWageAmount",
                ContextKeywords = ["মোট বকেয়া", "মজুরি", "দাবি", "টাকা"],
                Notes = "Total amount of unpaid wages claimed."
            },
            new()
            {
                QuestionId = "Q-WAGE-007",
                Category = "financial_details",
                QuestionBangla = "আপনার কোনো বকেয়া ওভারটাইম ভাতা পাওনা থাকলে তার পরিমাণ কত?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "OVERTIME_UNPAID"],
                Priority = 70,
                IsActive = true,
                RequiredFactKey = "overtimeDue",
                ContextKeywords = ["ওভারটাইম", "ভাতা", "টাকা", "বকেয়া"],
                Notes = "Overtime allowance outstanding."
            },
            new()
            {
                QuestionId = "Q-WAGE-008",
                Category = "incident_description",
                QuestionBangla = "আপনাকে চাকরি থেকে অব্যাহতি বা বরখাস্ত করা হয়ে থাকলে তার তারিখ ও কারণ কী?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNLAWFUL_TERMINATION"],
                Priority = 80,
                IsActive = true,
                RequiredFactKey = "terminationDetails",
                ContextKeywords = ["বরখাস্ত", "অব্যাহতি", "ছাঁটাই", "তারিখ", "কারণ"],
                Notes = "Termination or retrenchment date and grounds."
            },
            new()
            {
                QuestionId = "Q-WAGE-009",
                Category = "evidence_documents",
                QuestionBangla = "আপনার নিকট কী কী প্রামাণ্য নথি (যেমন: নিয়োগপত্র, আইডি কার্ড, পে-স্লিপ, হাজিরা খাতা) রয়েছে?",
                ParentCategoryCodes = ["LABOUR_WAGE"],
                RelatedIncidentTypes = ["LABOUR_WAGE", "UNPAID_WAGES"],
                Priority = 90,
                IsActive = true,
                RequiredFactKey = "evidenceList",
                ContextKeywords = ["নিয়োগপত্র", "আইডি কার্ড", "পে-স্লিপ", "হাজিরা খাতা", "প্রমাণ"],
                Notes = "Employment and wage documentary evidence."
            },

            // =========================================================================
            // ৪. জমি ও সম্পত্তি বিরোধ সারসংক্ষেপ টেমপ্লেট (LAND_PROPERTY)
            // =========================================================================
            new()
            {
                QuestionId = "Q-LAND-001",
                Category = "applicant",
                QuestionBangla = "বঞ্চিত স্বত্বাধিকারী বা আবেদনকারীর পূর্ণ নাম কী?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "PROPERTY_DEPRIVATION", "WOMEN_INHERITANCE_DENIAL"],
                Priority = 10,
                IsActive = true,
                RequiredFactKey = "applicant",
                ContextKeywords = ["নাম", "আবেদনকারী", "স্বত্বাধিকারী"],
                Notes = "Deprived owner / applicant name."
            },
            new()
            {
                QuestionId = "Q-LAND-002",
                Category = "perpetrator",
                QuestionBangla = "সম্পত্তি দখলদার বা অভিযুক্ত প্রতিপক্ষের পূর্ণ নাম কী?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "PROPERTY_DEPRIVATION"],
                Priority = 20,
                IsActive = true,
                RequiredFactKey = "respondent",
                ContextKeywords = ["দখলদার", "অভিযুক্ত", "প্রতিপক্ষ", "নাম"],
                Notes = "Land grabber / opposing party name."
            },
            new()
            {
                QuestionId = "Q-LAND-003",
                Category = "incident_description",
                QuestionBangla = "বিরোধপূর্ণ জমির তফসিল (মৌজা, খতিয়ান, দাগ নম্বর ও জমির পরিমাণ) উল্লেখ করুন।",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "PROPERTY_DEPRIVATION"],
                Priority = 30,
                IsActive = true,
                RequiredFactKey = "propertyDescription",
                ContextKeywords = ["তফসিল", "মৌজা", "খতিয়ান", "দাগ", "পরিমাণ", "জমি"],
                Notes = "Schedule of land (Mouza, Khatian, Dag, Area)."
            },
            new()
            {
                QuestionId = "Q-LAND-004",
                Category = "evidence_documents",
                QuestionBangla = "জমিটিতে আপনার মালিকানার প্রধান সূত্র কী (যেমন: পৈতৃক, ক্রয়, হেবা নাকি দেনমোহর)?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "WOMEN_INHERITANCE_DENIAL", "WOMEN_PROPERTY_RIGHT_DISPUTE"],
                Priority = 40,
                IsActive = true,
                RequiredFactKey = "ownershipType",
                ContextKeywords = ["মালিকানা", "সূত্র", "পৈতৃক", "ক্রয়", "হেবা", "দেনমোহর"],
                Notes = "Basis of legal title."
            },
            new()
            {
                QuestionId = "Q-LAND-005",
                Category = "incident_description",
                QuestionBangla = "আপনাকে কীভাবে অধিকার বঞ্চিত বা বেদখল করা হয়েছে তার বিবরণ দিন।",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "PROPERTY_DEPRIVATION"],
                Priority = 50,
                IsActive = true,
                RequiredFactKey = "deprivationNature",
                ContextKeywords = ["বেদখল", "বঞ্চিত", "অধিকার", "বিবরণ"],
                Notes = "Narrative of illegal dispossession or denial of rights."
            },
            new()
            {
                QuestionId = "Q-LAND-006",
                Category = "financial_details",
                QuestionBangla = "উক্ত সম্পত্তিতে আপনার আইনগত প্রাপ্য হিস্যা বা অংশের পরিমাণ কত?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "WOMEN_INHERITANCE_DENIAL"],
                Priority = 60,
                IsActive = true,
                RequiredFactKey = "inheritanceShare",
                ContextKeywords = ["হিস্যা", "অংশ", "ভাগ", "প্রাপ্য"],
                Notes = "Legal share or partition proportion."
            },
            new()
            {
                QuestionId = "Q-LAND-007",
                Category = "threat_details",
                QuestionBangla = "জমি জোরপূর্বক লিখে নেওয়ার জন্য কোনো হুমকি বা চাপ দেওয়া হচ্ছে কি?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY", "FORCED_PROPERTY_TRANSFER_PRESSURE"],
                Priority = 70,
                IsActive = true,
                RequiredFactKey = "threatDetails",
                ContextKeywords = ["হুমকি", "চাপ", "জোরপূর্বক", "লিখে নেওয়া"],
                Notes = "Threats and coercion for forced deed signing."
            },
            new()
            {
                QuestionId = "Q-LAND-008",
                Category = "evidence_documents",
                QuestionBangla = "জমির স্বপক্ষে আপনার নিকট কী কী দলিল-দস্তাবেজ (যেমন: খতিয়ান, পরচা, খাজনা দাখিলা) রয়েছে?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY"],
                Priority = 80,
                IsActive = true,
                RequiredFactKey = "evidenceList",
                ContextKeywords = ["দলিল", "খতিয়ান", "পরচা", "খাজনা দাখিলা", "কাগজ"],
                Notes = "Title deeds and land revenue documents."
            },
            new()
            {
                QuestionId = "Q-LAND-009",
                Category = "witness_details",
                QuestionBangla = "এই জমি সংক্রান্ত বিষয়ে পূর্বে কোনো স্থানীয় শালিস বা মীমাংসার উদ্যোগ নেওয়া হয়েছিল কি?",
                ParentCategoryCodes = ["LAND_PROPERTY"],
                RelatedIncidentTypes = ["LAND_PROPERTY"],
                Priority = 90,
                IsActive = true,
                RequiredFactKey = "localArbitration",
                ContextKeywords = ["শালিস", "মীমাংসা", "উদ্যোগ", "বৈঠক"],
                Notes = "Prior village mediation or settlement efforts."
            },

            // =========================================================================
            // ৫. নারী ও শিশু সুরক্ষা সারসংক্ষেপ টেমপ্লেট (WOMEN_CHILD_PROTECTION)
            // =========================================================================
            new()
            {
                QuestionId = "Q-PROT-001",
                Category = "applicant",
                QuestionBangla = "আবেদনকারী মা বা অভিভাবকের পূর্ণ নাম কী?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "CHILD_CUSTODY_DISPUTE"],
                Priority = 10,
                IsActive = true,
                RequiredFactKey = "applicant",
                ContextKeywords = ["মা", "অভিভাবক", "নাম", "আবেদনকারী"],
                Notes = "Mother / guardian full name."
            },
            new()
            {
                QuestionId = "Q-PROT-002",
                Category = "perpetrator",
                QuestionBangla = "সন্তানে বা চলাফেরায় বাধা প্রদানকারী প্রতিপক্ষের পূর্ণ নাম কী?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "CHILD_VISITATION_OBSTRUCTION", "MOVEMENT_RESTRICTION"],
                Priority = 20,
                IsActive = true,
                RequiredFactKey = "respondent",
                ContextKeywords = ["প্রতিপক্ষ", "বাধা প্রদানকারী", "নাম"],
                Notes = "Opposing party obstructing custody/movement."
            },
            new()
            {
                QuestionId = "Q-PROT-003",
                Category = "incident_description",
                QuestionBangla = "সন্তানদের নাম, বয়স এবং তারা বর্তমানে কার কাছে বা কোথায় অবস্থান করছে?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "CHILD_CUSTODY_DISPUTE"],
                Priority = 30,
                IsActive = true,
                RequiredFactKey = "childDetails",
                ContextKeywords = ["সন্তান", "নাম", "বয়স", "অবস্থান", "বাচ্চা"],
                Notes = "Children's names, ages, and current physical custody."
            },
            new()
            {
                QuestionId = "Q-PROT-004",
                Category = "threat_details",
                QuestionBangla = "সন্তানদের হেফাজত বা তাদের সাথে দেখা-সাক্ষাৎ করার ক্ষেত্রে কী ধরনের বাধা দেওয়া হচ্ছে?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "CHILD_CUSTODY_DISPUTE", "CHILD_VISITATION_OBSTRUCTION"],
                Priority = 40,
                IsActive = true,
                RequiredFactKey = "custodyDisputeNature",
                ContextKeywords = ["হেফাজত", "দেখা-সাক্ষাৎ", "বাধা", "কাস্টডি"],
                Notes = "Nature of child visitation or custody denial."
            },
            new()
            {
                QuestionId = "Q-PROT-005",
                Category = "injury_details",
                QuestionBangla = "আপনার বা আপনার সন্তানকে প্রয়োজনীয় চিকিৎসা সেবা নিতে কীভাবে বাধা দেওয়া হয়েছে?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "MEDICAL_OBSTRUCTION"],
                Priority = 50,
                IsActive = true,
                RequiredFactKey = "medicalObstruction",
                ContextKeywords = ["চিকিৎসা", "সেবা", "বাধা", "ডাক্তার", "হাসপাতাল"],
                Notes = "Denial of access to necessary medical treatment."
            },
            new()
            {
                QuestionId = "Q-PROT-006",
                Category = "threat_details",
                QuestionBangla = "আপনাকে কি অন্যায়ভাবে গৃহবন্দি বা চলাফেরায় কোনো কঠোর নিষেধাজ্ঞা আরোপ করা হয়েছে?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "MOVEMENT_RESTRICTION", "FORCED_CONFINEMENT"],
                Priority = 60,
                IsActive = true,
                RequiredFactKey = "movementRestriction",
                ContextKeywords = ["গৃহবন্দি", "চলাফেরা", "নিষেধাজ্ঞা", "আটকে"],
                Notes = "Wrongful confinement and movement restriction."
            },
            new()
            {
                QuestionId = "Q-PROT-007",
                Category = "financial_details",
                QuestionBangla = "মৌলিক চাহিদা বা জীবনযাত্রার প্রয়োজনীয় খরচ না দিয়ে কীভাবে আর্থিক চাপ দেওয়া হচ্ছে?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "ECONOMIC_CONTROL"],
                Priority = 70,
                IsActive = true,
                RequiredFactKey = "economicControl",
                ContextKeywords = ["মৌলিক চাহিদা", "খরচ", "আর্থিক চাপ", "টাকা"],
                Notes = "Economic deprivation and coercive financial control."
            },
            new()
            {
                QuestionId = "Q-PROT-008",
                Category = "current_safety",
                QuestionBangla = "সন্তানরা বর্তমানে শারীরিক বা মানসিকভাবে কোনো তাৎক্ষণিক নিরাপত্তা ঝুঁকিতে আছে কি?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "CHILD_CUSTODY_DISPUTE"],
                Priority = 80,
                IsActive = true,
                RequiredFactKey = "childSafetyRisk",
                ContextKeywords = ["নিরাপত্তা ঝুঁকি", "তাৎক্ষণিক", "শারীরিক", "মানসিক"],
                Notes = "Immediate physical or psychological safety risk to minors."
            },
            new()
            {
                QuestionId = "Q-PROT-009",
                Category = "desired_relief",
                QuestionBangla = "আদালতের নিকট আপনি সন্তানদের হেফাজত ও সুরক্ষা সংক্রান্ত কী আদেশ চাচ্ছেন?",
                ParentCategoryCodes = ["WOMEN_CHILD_PROTECTION"],
                RelatedIncidentTypes = ["WOMEN_CHILD_PROTECTION", "CHILD_CUSTODY_DISPUTE"],
                Priority = 90,
                IsActive = true,
                RequiredFactKey = "desiredRelief",
                ContextKeywords = ["আদালত", "হেফাজত", "সুরক্ষা", "আদেশ", "প্রতিকার"],
                Notes = "Legal prayer for guardianship, custody, and protective order."
            },

            // =========================================================================
            // ৬. অন্যান্য আইনি সমস্যা সারসংক্ষেপ টেমপ্লেট (OTHER)
            // =========================================================================
            new()
            {
                QuestionId = "Q-OTHER-001",
                Category = "applicant",
                QuestionBangla = "আপনার পূর্ণ নাম কী?",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "MARRIAGE_FRAUD", "OTHER_ISSUE"],
                Priority = 10,
                IsActive = true,
                RequiredFactKey = "applicant",
                ContextKeywords = ["নাম", "আবেদনকারী"],
                Notes = "Applicant full name."
            },
            new()
            {
                QuestionId = "Q-OTHER-002",
                Category = "perpetrator",
                QuestionBangla = "বিবাদী বা অপর পক্ষের পূর্ণ নাম কী?",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "MARRIAGE_FRAUD", "OTHER_ISSUE"],
                Priority = 20,
                IsActive = true,
                RequiredFactKey = "respondent",
                ContextKeywords = ["বিবাদী", "অপর পক্ষ", "নাম"],
                Notes = "Opposing party / respondent full name."
            },
            new()
            {
                QuestionId = "Q-OTHER-003",
                Category = "incident_description",
                QuestionBangla = "আপনার সাথে ঘটে যাওয়া মূল সমস্যা বা ঘটনার বিবরণ সংক্ষেপে লিখুন।",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "MARRIAGE_FRAUD", "OTHER_ISSUE"],
                Priority = 30,
                IsActive = true,
                RequiredFactKey = "incidentDescription",
                ContextKeywords = ["সমস্যা", "ঘটনা", "বিবরণ", "ঘটনাটি"],
                Notes = "Core narrative of the incident or dispute."
            },
            new()
            {
                QuestionId = "Q-OTHER-004",
                Category = "incident_time",
                QuestionBangla = "ঘটনাটি কত তারিখে এবং কোন স্থানে সংঘটিত হয়েছিল?",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "MARRIAGE_FRAUD", "OTHER_ISSUE"],
                Priority = 40,
                IsActive = true,
                RequiredFactKey = "incidentDate",
                ContextKeywords = ["তারিখ", "স্থান", "কখন", "কোথায়"],
                Notes = "Date and location of occurrence."
            },
            new()
            {
                QuestionId = "Q-OTHER-005",
                Category = "incident_description",
                QuestionBangla = "ঘটনার মধ্যে কোনো মিথ্যা তথ্য বা প্রতারণা (যেমন: বৈবাহিক তথ্য গোপন) থাকলে তা উল্লেখ করুন।",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "MARRIAGE_FRAUD"],
                Priority = 50,
                IsActive = true,
                RequiredFactKey = "fraudDetails",
                ContextKeywords = ["মিথ্যা তথ্য", "প্রতারণা", "গোপন", "জালিয়াতি"],
                Notes = "Details of fraud or concealment of marital facts."
            },
            new()
            {
                QuestionId = "Q-OTHER-006",
                Category = "financial_details",
                QuestionBangla = "এই ঘটনার কারণে আপনার আনুমানিক কত টাকার আর্থিক বা মানসিকভাবে ক্ষতি হয়েছে?",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "OTHER_ISSUE"],
                Priority = 60,
                IsActive = true,
                RequiredFactKey = "financialLoss",
                ContextKeywords = ["আর্থিক ক্ষতি", "মানসিক ক্ষতি", "টাকা", "ক্ষতি"],
                Notes = "Quantification of financial or emotional damages."
            },
            new()
            {
                QuestionId = "Q-OTHER-007",
                Category = "evidence_documents",
                QuestionBangla = "আপনার দাবির পক্ষে কোনো কাগজপত্র বা লিখিত প্রমাণাদি আছে কি?",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "OTHER_ISSUE"],
                Priority = 70,
                IsActive = true,
                RequiredFactKey = "evidenceList",
                ContextKeywords = ["কাগজপত্র", "লিখিত প্রমাণাদি", "দলিল", "রশিদ"],
                Notes = "Available documents or written proof."
            },
            new()
            {
                QuestionId = "Q-OTHER-008",
                Category = "desired_relief",
                QuestionBangla = "এই আইনি সমস্যা সমাধানে আপনি আমাদের নিকট থেকে কী ধরনের সহায়তা আশা করছেন?",
                ParentCategoryCodes = ["OTHER"],
                RelatedIncidentTypes = ["OTHER", "OTHER_ISSUE"],
                Priority = 80,
                IsActive = true,
                RequiredFactKey = "desiredRelief",
                ContextKeywords = ["সহায়তা", "প্রতিকার", "সমাধান", "পরামর্শ"],
                Notes = "Desired legal assistance or counsel."
            }
        };

        foreach (var item in items)
        {
            item.Id = Guid.NewGuid().ToString();
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _context.QuestionBank.InsertManyAsync(items, cancellationToken: ct);

        _logger.LogInformation("Seeded/Synchronized {Count} curated legal questions mapped to parent categories in question_bank collection.", items.Count);
    }

    private async Task SeedParentCategoriesAsync(CancellationToken ct)
    {
        // 1. Ensure unique index on Code
        try
        {
            var indexKeys = Builders<ParentIncidentCategoryModel>.IndexKeys.Ascending(x => x.Code);
            var indexOptions = new CreateIndexOptions { Unique = true, Name = "ux_parent_categories_code" };
            await _context.ParentIncidentCategories.Indexes.CreateOneAsync(
                new CreateIndexModel<ParentIncidentCategoryModel>(indexKeys, indexOptions), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index creation on parent_incident_categories.code checked/handled.");
        }

        // 2. Define the 6 core Parent Incident Categories
        var categories = new List<ParentIncidentCategoryModel>
        {
            new()
            {
                Code = "DOWER_MAINTENANCE",
                Name = "দেনমোহর / ভরণপোষণ",
                NameEn = "Dower & Maintenance",
                Description = "দেনমোহর পরিশোধ না করা, স্ত্রী ও সন্তানের ভরণপোষণ, বিবাহবিচ্ছেদ পরবর্তী পাওনা সংক্রান্ত আইনি বিরোধ।",
                Icon = "scale",
                Order = 1,
                IsActive = true
            },
            new()
            {
                Code = "DOWRY_DOMESTIC_VIOLENCE",
                Name = "যৌতুক / পারিবারিক সহিংসতা",
                NameEn = "Dowry & Domestic Violence",
                Description = "যৌতুক দাবি, যৌতুক প্রদান সংক্রান্ত চাপ, শারীরিক নির্যাতন, বাড়ি থেকে উচ্ছেদ ও পারিবারিক সহিংসতা।",
                Icon = "shield-alert",
                Order = 2,
                IsActive = true
            },
            new()
            {
                Code = "LABOUR_WAGE",
                Name = "বকেয়া মজুরি / শ্রম",
                NameEn = "Labour & Unpaid Wages",
                Description = "বকেয়া মজুরি, ওভারটাইম বকেয়া, অন্যায়ভাবে চাকরিচ্যুতি ও শ্রম আইন সংক্রান্ত বিরোধ।",
                Icon = "briefcase",
                Order = 3,
                IsActive = true
            },
            new()
            {
                Code = "LAND_PROPERTY",
                Name = "জমি / সম্পত্তি বিরোধ",
                NameEn = "Land & Property Dispute",
                Description = "সম্পত্তি থেকে বঞ্চিত করা, নারীর পৈতৃক ও বৈবাহিক সম্পত্তির অধিকার হরণ, জোরপূর্বক সম্পত্তি হস্তান্তরের চাপ।",
                Icon = "home",
                Order = 4,
                IsActive = true
            },
            new()
            {
                Code = "WOMEN_CHILD_PROTECTION",
                Name = "নারী ও শিশু সুরক্ষা",
                NameEn = "Women & Child Protection",
                Description = "সন্তানের হেফাজত ও দেখা-সাক্ষাতে বাধা, চিকিৎসার অধিকার থেকে বঞ্চিত করা, চলাফেরা ও অর্থনৈতিক অন্যায় নিয়ন্ত্রণ।",
                Icon = "users",
                Order = 5,
                IsActive = true
            },
            new()
            {
                Code = "OTHER",
                Name = "অন্য সমস্যা",
                NameEn = "Other Legal Matters",
                Description = "বিবাহ-সংক্রান্ত প্রতারণা, মিথ্যা তথ্য প্রদান এবং অন্যান্য যেকোনো আইনি পরামর্শ ও সহায়তা।",
                Icon = "help-circle",
                Order = 6,
                IsActive = true
            }
        };

        foreach (var cat in categories)
        {
            var existing = await _context.ParentIncidentCategories.Find(x => x.Code == cat.Code).FirstOrDefaultAsync(ct);
            if (existing == null)
            {
                cat.CreatedAt = DateTime.UtcNow;
                cat.UpdatedAt = DateTime.UtcNow;
                await _context.ParentIncidentCategories.InsertOneAsync(cat, cancellationToken: ct);
            }
            else
            {
                var update = Builders<ParentIncidentCategoryModel>.Update
                    .Set(x => x.Name, cat.Name)
                    .Set(x => x.NameEn, cat.NameEn)
                    .Set(x => x.Description, cat.Description)
                    .Set(x => x.Icon, cat.Icon)
                    .Set(x => x.Order, cat.Order)
                    .Set(x => x.IsActive, cat.IsActive)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);

                await _context.ParentIncidentCategories.UpdateOneAsync(x => x.Code == cat.Code, update, cancellationToken: ct);
            }
        }

        _logger.LogInformation("Seeded/Verified {Count} parent incident categories.", categories.Count);
    }

    private async Task EnsureIncidentTypeParentMappingsAsync(CancellationToken ct)
    {
        // 1. Ensure index on incident_types.parentCategoryCode
        try
        {
            var indexKeys = Builders<IncidentTypeModel>.IndexKeys.Ascending(x => x.ParentCategoryCode);
            var indexOptions = new CreateIndexOptions { Name = "ix_incident_types_parent_code" };
            await _context.IncidentTypes.Indexes.CreateOneAsync(
                new CreateIndexModel<IncidentTypeModel>(indexKeys, indexOptions), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index creation on incident_types.parentCategoryCode checked/handled.");
        }

        // 2. Ensure supporting incident types exist for all parent categories (e.g. LABOUR_WAGE, OTHER)
        var additionalIncidentTypes = new List<IncidentTypeModel>
        {
            new()
            {
                Code = "UNPAID_WAGES",
                Name = "বকেয়া মজুরি / বেতন বকেয়া",
                NameEn = "Unpaid Wages / Salary Dues",
                Category = LegalCategories.UnpaidWages,
                ParentCategoryCode = "LABOUR_WAGE",
                IsActive = true
            },
            new()
            {
                Code = "OVERTIME_UNPAID",
                Name = "বকেয়া ওভারটাইম ভাতা",
                NameEn = "Unpaid Overtime Allowance",
                Category = LegalCategories.UnpaidWages,
                ParentCategoryCode = "LABOUR_WAGE",
                IsActive = true
            },
            new()
            {
                Code = "UNLAWFUL_TERMINATION",
                Name = "বেআইনি চাকরিচ্যুতি / ছাঁটাই",
                NameEn = "Unlawful Termination / Dismissal",
                Category = LegalCategories.UnpaidWages,
                ParentCategoryCode = "LABOUR_WAGE",
                IsActive = true
            },
            new()
            {
                Code = "OTHER_ISSUE",
                Name = "অন্যান্য আইনি জটিলতা",
                NameEn = "Other Legal Issues",
                Category = "OTHER",
                ParentCategoryCode = "OTHER",
                IsActive = true
            }
        };

        foreach (var item in additionalIncidentTypes)
        {
            var exists = await _context.IncidentTypes.Find(x => x.Code == item.Code).AnyAsync(ct);
            if (!exists)
            {
                await _context.IncidentTypes.InsertOneAsync(item, cancellationToken: ct);
                _logger.LogInformation("Added complementary incident type: {Code}", item.Code);
            }
        }

        // 3. Mapping table: Incident Type Code -> Parent Category Code
        var codeToParentMap = new Dictionary<string, string>
        {
            // DOWER_MAINTENANCE
            ["DOWER_NONPAYMENT"] = "DOWER_MAINTENANCE",
            ["MAINTENANCE_NONPAYMENT"] = "DOWER_MAINTENANCE",
            ["CHILD_MAINTENANCE_NONPAYMENT"] = "DOWER_MAINTENANCE",
            ["POST_DIVORCE_DUES_NONPAYMENT"] = "DOWER_MAINTENANCE",

            // DOWRY_DOMESTIC_VIOLENCE
            ["DOWRY_DEMAND"] = "DOWRY_DOMESTIC_VIOLENCE",
            ["DOWRY_PAYMENT"] = "DOWRY_DOMESTIC_VIOLENCE",
            ["DOMESTIC_PHYSICAL_ABUSE"] = "DOWRY_DOMESTIC_VIOLENCE",
            ["HUSBAND_EVICTION_ATTEMPT"] = "DOWRY_DOMESTIC_VIOLENCE",
            ["DEATH_THREAT"] = "DOWRY_DOMESTIC_VIOLENCE",
            ["PHYSICAL_HARM_THREAT"] = "DOWRY_DOMESTIC_VIOLENCE",
            ["FORCED_CONFINEMENT"] = "DOWRY_DOMESTIC_VIOLENCE",

            // LABOUR_WAGE
            ["UNPAID_WAGES"] = "LABOUR_WAGE",
            ["OVERTIME_UNPAID"] = "LABOUR_WAGE",
            ["UNLAWFUL_TERMINATION"] = "LABOUR_WAGE",

            // LAND_PROPERTY
            ["PROPERTY_DEPRIVATION"] = "LAND_PROPERTY",
            ["WOMEN_INHERITANCE_DENIAL"] = "LAND_PROPERTY",
            ["WOMEN_PROPERTY_RIGHT_DISPUTE"] = "LAND_PROPERTY",
            ["FORCED_PROPERTY_TRANSFER_PRESSURE"] = "LAND_PROPERTY",

            // WOMEN_CHILD_PROTECTION
            ["CHILD_CUSTODY_DISPUTE"] = "WOMEN_CHILD_PROTECTION",
            ["CHILD_VISITATION_OBSTRUCTION"] = "WOMEN_CHILD_PROTECTION",
            ["MEDICAL_OBSTRUCTION"] = "WOMEN_CHILD_PROTECTION",
            ["MOVEMENT_RESTRICTION"] = "WOMEN_CHILD_PROTECTION",
            ["ECONOMIC_CONTROL"] = "WOMEN_CHILD_PROTECTION",

            // OTHER
            ["MARRIAGE_FRAUD"] = "OTHER",
            ["OTHER_ISSUE"] = "OTHER"
        };

        foreach (var (typeCode, parentCode) in codeToParentMap)
        {
            var update = Builders<IncidentTypeModel>.Update.Set(x => x.ParentCategoryCode, parentCode);
            await _context.IncidentTypes.UpdateOneAsync(
                x => x.Code == typeCode && x.ParentCategoryCode != parentCode,
                update,
                cancellationToken: ct);
        }

        _logger.LogInformation("Verified parent category mappings for incident types.");
    }

    private async Task SeedCaseSummaryTemplatesAsync(CancellationToken ct)
    {
        // 1. Ensure unique index on parentCategoryCode
        try
        {
            var indexKeys = Builders<CaseSummaryTemplateModel>.IndexKeys.Ascending(x => x.ParentCategoryCode);
            var indexOptions = new CreateIndexOptions { Unique = true, Name = "ux_summary_templates_parent_code" };
            await _context.CaseSummaryTemplates.Indexes.CreateOneAsync(
                new CreateIndexModel<CaseSummaryTemplateModel>(indexKeys, indexOptions), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Index creation on case_summary_templates.parentCategoryCode checked/handled.");
        }

        // 2. Define the 6 active configuration-driven templates
        var templates = new List<CaseSummaryTemplateModel>
        {
            // === 1. DOWER_MAINTENANCE ===
            new()
            {
                ParentCategoryCode = "DOWER_MAINTENANCE",
                Name = "দেনমোহর ও ভরণপোষণ সারসংক্ষেপ টেমপ্লেট",
                NameEn = "Dower & Maintenance Summary Template",
                Description = "দেনমোহর, স্ত্রী ও সন্তানের বকেয়া ভরণপোষণ এবং বিবাহবিচ্ছেদ সংক্রান্ত পাওনা আদায়ের প্রয়োজনীয় তথ্যাদি।",
                Version = 1,
                IsActive = true,
                Fields =
                [
                    new()
                    {
                        Key = "applicant",
                        LabelBn = "আবেদনকারীর নাম / স্ত্রী",
                        LabelEn = "Applicant / Wife Name",
                        Order = 1,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["applicant", "INCIDENT_VICTIM", "CLIENT_NAME"]
                    },
                    new()
                    {
                        Key = "respondent",
                        LabelBn = "বিবাদীর নাম / স্বামী",
                        LabelEn = "Respondent / Husband Name",
                        Order = 2,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["respondent", "INCIDENT_ACCUSED", "OPPOSING_PARTY"]
                    },
                    new()
                    {
                        Key = "marriageDate",
                        LabelBn = "বিবাহের তারিখ / সময়কাল",
                        LabelEn = "Marriage Date / Period",
                        Order = 3,
                        IsRequired = false,
                        FieldType = "Date",
                        SourceQuestionCodes = ["marriageDate", "MARRIAGE_DATE", "INCIDENT_TIME"]
                    },
                    new()
                    {
                        Key = "dowerAmount",
                        LabelBn = "দেনমোহরের মোট ধার্যকৃত অঙ্ক",
                        LabelEn = "Total Fixed Dower Amount",
                        Order = 4,
                        IsRequired = true,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["dowerAmount", "DOWER_TOTAL", "DOWER_AMOUNT"]
                    },
                    new()
                    {
                        Key = "paidDowerAmount",
                        LabelBn = "পরিশোধিত দেনমোহর",
                        LabelEn = "Paid Dower Amount",
                        Order = 5,
                        IsRequired = false,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["paidDowerAmount", "DOWER_PAID"]
                    },
                    new()
                    {
                        Key = "unpaidDowerAmount",
                        LabelBn = "বকেয়া দেনমোহর",
                        LabelEn = "Unpaid Dower Amount",
                        Order = 6,
                        IsRequired = true,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["unpaidDowerAmount", "claimedAmount", "DOWER_DUE"]
                    },
                    new()
                    {
                        Key = "maintenanceDue",
                        LabelBn = "বকেয়া ভরণপোষণ / খোরপোষের দাবি",
                        LabelEn = "Due Maintenance / Alimony",
                        Order = 7,
                        IsRequired = false,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["maintenanceDue", "MAINTENANCE_DUE", "MAINTENANCE_AMOUNT"]
                    },
                    new()
                    {
                        Key = "childCount",
                        LabelBn = "সন্তানের সংখ্যা ও ভরণপোষণ",
                        LabelEn = "Child Count & Child Support",
                        Order = 8,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["childCount", "CHILD_COUNT", "CHILD_MAINTENANCE"]
                    },
                    new()
                    {
                        Key = "evidenceList",
                        LabelBn = "প্রমাণাদি ও নথিপত্র (যেমন: কাবিননামা/শালিসনামা)",
                        LabelEn = "Evidence & Documents (Nikahnama/Settlement)",
                        Order = 9,
                        IsRequired = false,
                        FieldType = "EvidenceList",
                        SourceQuestionCodes = ["evidenceList", "EVIDENCE_CHECK", "evidence"]
                    },
                    new()
                    {
                        Key = "desiredRelief",
                        LabelBn = "প্রার্থিত আইনি প্রতিকার",
                        LabelEn = "Desired Legal Relief",
                        Order = 10,
                        IsRequired = true,
                        FieldType = "Choice",
                        SourceQuestionCodes = ["desiredRelief", "RELIEF_PRAYER", "Q-RELIEF-001"]
                    }
                ]
            },

            // === 2. DOWRY_DOMESTIC_VIOLENCE ===
            new()
            {
                ParentCategoryCode = "DOWRY_DOMESTIC_VIOLENCE",
                Name = "যৌতুক ও পারিবারিক সহিংসতা সারসংক্ষেপ টেমপ্লেট",
                NameEn = "Dowry & Domestic Violence Summary Template",
                Description = "যৌতুক দাবি, শারীরিক নির্যাতন ও তাৎক্ষণিক পারিবারিক সুরক্ষা সংক্রান্ত মূল তথ্যাদি।",
                Version = 1,
                IsActive = true,
                Fields =
                [
                    new()
                    {
                        Key = "applicant",
                        LabelBn = "নির্যাতনের শিকার / আবেদনকারী",
                        LabelEn = "Victim / Applicant Name",
                        Order = 1,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["applicant", "INCIDENT_VICTIM"]
                    },
                    new()
                    {
                        Key = "respondent",
                        LabelBn = "অভিযুক্ত ব্যক্তি / নির্যাতনকারী",
                        LabelEn = "Accused / Abuser Name",
                        Order = 2,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["respondent", "INCIDENT_ACCUSED"]
                    },
                    new()
                    {
                        Key = "dowryDemandAmount",
                        LabelBn = "দাবিকৃত যৌতুকের বিবরণ ও অঙ্ক",
                        LabelEn = "Dowry Demand Amount & Description",
                        Order = 3,
                        IsRequired = true,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["dowryDemandAmount", "claimedAmount", "DOWRY_DEMAND_AMOUNT"]
                    },
                    new()
                    {
                        Key = "dowryPaidAmount",
                        LabelBn = "পূর্বে প্রদত্ত যৌতুকের পরিমাণ",
                        LabelEn = "Previously Paid Dowry Amount",
                        Order = 4,
                        IsRequired = false,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["dowryPaidAmount", "DOWRY_PAID"]
                    },
                    new()
                    {
                        Key = "abuseType",
                        LabelBn = "শারীরিক / মানসিক নির্যাতনের ধরন",
                        LabelEn = "Type of Physical / Mental Abuse",
                        Order = 5,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["abuseType", "DOMESTIC_ABUSE_TYPE", "ABUSE_TYPE"]
                    },
                    new()
                    {
                        Key = "injuryDetails",
                        LabelBn = "শারীরিক আঘাত ও চিকিৎসার বিবরণ",
                        LabelEn = "Injury & Medical Treatment Details",
                        Order = 6,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["injuryDetails", "INJURY_REPORT", "MEDICAL_TREATMENT"]
                    },
                    new()
                    {
                        Key = "incidentLocation",
                        LabelBn = "ঘটনাস্থল / বাড়ি থেকে উচ্ছেদের বিবরণ",
                        LabelEn = "Incident Location / Eviction Attempt",
                        Order = 7,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["incidentLocation", "INCIDENT_LOCATION", "EVICTION_DETAILS"]
                    },
                    new()
                    {
                        Key = "immediateThreat",
                        LabelBn = "জরুরি নিরাপত্তা ঝুঁকি / হুমকি",
                        LabelEn = "Immediate Threat / Safety Risk",
                        Order = 8,
                        IsRequired = true,
                        FieldType = "Choice",
                        SourceQuestionCodes = ["immediateThreat", "safetyRisk", "INCIDENT_EMERGENCY"]
                    },
                    new()
                    {
                        Key = "policeReport",
                        LabelBn = "থানায় জিডি বা পুলিশ অভিযোগের তথ্য",
                        LabelEn = "Police GD / Complaint Details",
                        Order = 9,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["policeReport", "gdDetails", "PREVIOUS_REPORT"]
                    },
                    new()
                    {
                        Key = "witnessList",
                        LabelBn = "সাক্ষী ও সুরক্ষামূলক প্রমাণাদি",
                        LabelEn = "Witnesses & Corroborating Evidence",
                        Order = 10,
                        IsRequired = false,
                        FieldType = "EvidenceList",
                        SourceQuestionCodes = ["witnessList", "evidenceList", "EVIDENCE_CHECK"]
                    }
                ]
            },

            // === 3. LABOUR_WAGE ===
            new()
            {
                ParentCategoryCode = "LABOUR_WAGE",
                Name = "বকেয়া মজুরি ও শ্রম বিরোধ সারসংক্ষেপ টেমপ্লেট",
                NameEn = "Labour & Unpaid Wages Summary Template",
                Description = "বকেয়া বেতন, ওভারটাইম ভাতা, সার্ভিস বেনিফিট ও বেআইনি বরখাস্ত সংক্রান্ত তথ্য।",
                Version = 1,
                IsActive = true,
                Fields =
                [
                    new()
                    {
                        Key = "applicant",
                        LabelBn = "শ্রমিক / কর্মচারীর নাম",
                        LabelEn = "Worker / Employee Name",
                        Order = 1,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["applicant", "WORKER_NAME"]
                    },
                    new()
                    {
                        Key = "employerName",
                        LabelBn = "মালিক / প্রতিষ্ঠানের নাম ও ঠিকানা",
                        LabelEn = "Employer / Company Name & Address",
                        Order = 2,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["employerName", "respondent", "COMPANY_NAME"]
                    },
                    new()
                    {
                        Key = "jobDesignation",
                        LabelBn = "পদবী / কাজের বিবরণ",
                        LabelEn = "Job Designation / Role",
                        Order = 3,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["jobDesignation", "DESIGNATION"]
                    },
                    new()
                    {
                        Key = "monthlySalary",
                        LabelBn = "মাসিক মূল মজুরি / দৈনিক হার",
                        LabelEn = "Monthly Wage / Daily Rate",
                        Order = 4,
                        IsRequired = true,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["monthlySalary", "WAGE_RATE"]
                    },
                    new()
                    {
                        Key = "unpaidMonths",
                        LabelBn = "বকেয়া মাসের সংখ্যা / সময়কাল",
                        LabelEn = "Number of Unpaid Months / Duration",
                        Order = 5,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["unpaidMonths", "UNPAID_PERIOD"]
                    },
                    new()
                    {
                        Key = "unpaidWageAmount",
                        LabelBn = "মোট বকেয়া মজুরির দাবি",
                        LabelEn = "Total Unpaid Wage Claimed",
                        Order = 6,
                        IsRequired = true,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["unpaidWageAmount", "claimedAmount", "TOTAL_WAGE_DUE"]
                    },
                    new()
                    {
                        Key = "overtimeDue",
                        LabelBn = "বকেয়া ওভারটাইম ভাতা",
                        LabelEn = "Overtime Due Amount",
                        Order = 7,
                        IsRequired = false,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["overtimeDue", "OVERTIME_AMOUNT"]
                    },
                    new()
                    {
                        Key = "terminationDetails",
                        LabelBn = "চাকরিচ্যুতি / ছাঁটাইয়ের তারিখ ও ধরন",
                        LabelEn = "Termination / Dismissal Details",
                        Order = 8,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["terminationDetails", "TERMINATION_DATE"]
                    },
                    new()
                    {
                        Key = "evidenceList",
                        LabelBn = "প্রমাণাদি (আইডি কার্ড, পে-স্লিপ, হাজিরা খাতা)",
                        LabelEn = "Evidence (ID Card, Pay Slip, Attendance)",
                        Order = 9,
                        IsRequired = false,
                        FieldType = "EvidenceList",
                        SourceQuestionCodes = ["evidenceList", "EVIDENCE_CHECK"]
                    }
                ]
            },

            // === 4. LAND_PROPERTY ===
            new()
            {
                ParentCategoryCode = "LAND_PROPERTY",
                Name = "জমি ও সম্পত্তি বিরোধ সারসংক্ষেপ টেমপ্লেট",
                NameEn = "Land & Property Dispute Summary Template",
                Description = "পৈতৃক সম্পত্তি থেকে বঞ্চনা, জোরপূর্বক বেদখল ও নারীর সম্পত্তির অধিকার সংক্রান্ত মূল তথ্যাদি।",
                Version = 1,
                IsActive = true,
                Fields =
                [
                    new()
                    {
                        Key = "applicant",
                        LabelBn = "বঞ্চিত স্বত্বাধিকারী / আবেদনকারী",
                        LabelEn = "Claimant / Deprived Party Name",
                        Order = 1,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["applicant", "CLAIMANT_NAME"]
                    },
                    new()
                    {
                        Key = "respondent",
                        LabelBn = "দখলদার / অভিযুক্ত ব্যক্তি",
                        LabelEn = "Disputing / Accused Party Name",
                        Order = 2,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["respondent", "DISPUTED_PARTY"]
                    },
                    new()
                    {
                        Key = "propertyDescription",
                        LabelBn = "সম্পত্তির তফসিল (মৌজা, খতিয়ান, দাগ, পরিমাণ)",
                        LabelEn = "Property Description (Mouza, Khatian, Dag)",
                        Order = 3,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["propertyDescription", "LAND_LOCATION", "PROPERTY_DETAILS"]
                    },
                    new()
                    {
                        Key = "ownershipType",
                        LabelBn = "মালিকানার সূত্র (পৈতৃক / ক্রয় / হেবা / দেনমোহর)",
                        LabelEn = "Basis of Title (Inheritance/Purchase/Gift/Dower)",
                        Order = 4,
                        IsRequired = true,
                        FieldType = "Choice",
                        SourceQuestionCodes = ["ownershipType", "TITLE_TYPE"]
                    },
                    new()
                    {
                        Key = "deprivationNature",
                        LabelBn = "অধিকার হরণ বা বেদখলের প্রকৃতি",
                        LabelEn = "Nature of Deprivation / Dispossession",
                        Order = 5,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["deprivationNature", "DISPUTE_NATURE"]
                    },
                    new()
                    {
                        Key = "inheritanceShare",
                        LabelBn = "নারীর প্রাপ্য হিস্যা / অংশের বিবরণ",
                        LabelEn = "Inheritance Share Percentage / Proportion",
                        Order = 6,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["inheritanceShare", "SHARE_RATIO"]
                    },
                    new()
                    {
                        Key = "threatDetails",
                        LabelBn = "জোরপূর্বক হস্তান্তর বা হুমকির বিবরণ",
                        LabelEn = "Threat / Pressure for Forced Transfer",
                        Order = 7,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["threatDetails", "FORCED_TRANSFER_PRESSURE"]
                    },
                    new()
                    {
                        Key = "evidenceList",
                        LabelBn = "দলিল-দস্তাবেজ (খতিয়ান, পরচা, খাজনা দাখিলা)",
                        LabelEn = "Documents (Khatian, Porcha, Rent Receipt)",
                        Order = 8,
                        IsRequired = false,
                        FieldType = "EvidenceList",
                        SourceQuestionCodes = ["evidenceList", "TITLE_DEED", "EVIDENCE_CHECK"]
                    },
                    new()
                    {
                        Key = "localArbitration",
                        LabelBn = "স্থানীয় শালিস বা পূর্ববর্তী মীমাংসার বিবরণ",
                        LabelEn = "Local Settlement / Arbitration Record",
                        Order = 9,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["localArbitration", "PREVIOUS_ARBITRATION"]
                    }
                ]
            },

            // === 5. WOMEN_CHILD_PROTECTION ===
            new()
            {
                ParentCategoryCode = "WOMEN_CHILD_PROTECTION",
                Name = "নারী ও শিশু সুরক্ষা সারসংক্ষেপ টেমপ্লেট",
                NameEn = "Women & Child Protection Summary Template",
                Description = "সন্তানের অভিভাবকত্ব ও হেফাজত, দেখা-সাক্ষাতে বাধা, চিকিৎসাবঞ্চনা ও জরুরি শিশু সুরক্ষা।",
                Version = 1,
                IsActive = true,
                Fields =
                [
                    new()
                    {
                        Key = "applicant",
                        LabelBn = "মা / অভিভাবক / আবেদনকারী",
                        LabelEn = "Mother / Guardian Name",
                        Order = 1,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["applicant", "GUARDIAN_NAME"]
                    },
                    new()
                    {
                        Key = "respondent",
                        LabelBn = "প্রতিপক্ষ / বাধা প্রদানকারী",
                        LabelEn = "Opposing Party / Obstructer Name",
                        Order = 2,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["respondent", "OPPOSING_PARTY"]
                    },
                    new()
                    {
                        Key = "childDetails",
                        LabelBn = "সন্তানদের নাম, বয়স ও বর্তমান অবস্থান",
                        LabelEn = "Children Names, Ages & Current Location",
                        Order = 3,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["childDetails", "CHILD_INFO", "childCount"]
                    },
                    new()
                    {
                        Key = "custodyDisputeNature",
                        LabelBn = "হেফাজত বা দেখা-সাক্ষাতে বাধার বিবরণ",
                        LabelEn = "Custody / Visitation Obstruction Details",
                        Order = 4,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["custodyDisputeNature", "CUSTODY_OBSTRUCTION"]
                    },
                    new()
                    {
                        Key = "medicalObstruction",
                        LabelBn = "চিকিৎসা সেবা নিতে বাধা দেওয়ার বিবরণ",
                        LabelEn = "Obstruction to Medical Treatment",
                        Order = 5,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["medicalObstruction", "MEDICAL_DENIAL"]
                    },
                    new()
                    {
                        Key = "movementRestriction",
                        LabelBn = "চলাফেরায় অন্যায় বাধা বা গৃহবন্দি করার বিবরণ",
                        LabelEn = "Confinement / Movement Restriction Details",
                        Order = 6,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["movementRestriction", "CONFINEMENT_DETAILS"]
                    },
                    new()
                    {
                        Key = "economicControl",
                        LabelBn = "অর্থনৈতিক নিয়ন্ত্রণ বা মৌলিক চাহিদা থেকে বঞ্চনা",
                        LabelEn = "Economic Control / Deprivation",
                        Order = 7,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["economicControl", "FINANCIAL_DEPRIVATION"]
                    },
                    new()
                    {
                        Key = "childSafetyRisk",
                        LabelBn = "সন্তানের তাৎক্ষণিক নিরাপত্তা ঝুঁকি",
                        LabelEn = "Immediate Safety Risk to Child",
                        Order = 8,
                        IsRequired = true,
                        FieldType = "Choice",
                        SourceQuestionCodes = ["childSafetyRisk", "safetyRisk", "EMERGENCY_RISK"]
                    },
                    new()
                    {
                        Key = "desiredRelief",
                        LabelBn = "প্রার্থিত হেফাজত ও সুরক্ষা আদেশ",
                        LabelEn = "Desired Custody & Protection Order",
                        Order = 9,
                        IsRequired = true,
                        FieldType = "Choice",
                        SourceQuestionCodes = ["desiredRelief", "PROTECTION_PRAYER"]
                    }
                ]
            },

            // === 6. OTHER ===
            new()
            {
                ParentCategoryCode = "OTHER",
                Name = "অন্যান্য আইনি সমস্যা সারসংক্ষেপ টেমপ্লেট",
                NameEn = "Other Legal Issues Summary Template",
                Description = "বিবাহ প্রতারণা, মিথ্যা তথ্য প্রদান এবং অন্যান্য সাধারণ আইনি বিরোধের সারসংক্ষেপ।",
                Version = 1,
                IsActive = true,
                Fields =
                [
                    new()
                    {
                        Key = "applicant",
                        LabelBn = "আবেদনকারীর নাম",
                        LabelEn = "Applicant Name",
                        Order = 1,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["applicant", "INCIDENT_VICTIM"]
                    },
                    new()
                    {
                        Key = "respondent",
                        LabelBn = "বিবাদী বা অপর পক্ষ",
                        LabelEn = "Respondent Name",
                        Order = 2,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["respondent", "INCIDENT_ACCUSED"]
                    },
                    new()
                    {
                        Key = "incidentDescription",
                        LabelBn = "সমস্যা বা ঘটনার বিবরণ",
                        LabelEn = "Description of Problem / Incident",
                        Order = 3,
                        IsRequired = true,
                        FieldType = "Text",
                        SourceQuestionCodes = ["incidentDescription", "INCIDENT_DETAILS", "transcript"]
                    },
                    new()
                    {
                        Key = "incidentDate",
                        LabelBn = "ঘটনার সময়কাল ও স্থান",
                        LabelEn = "Incident Date & Location",
                        Order = 4,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["incidentDate", "INCIDENT_TIME", "INCIDENT_LOCATION"]
                    },
                    new()
                    {
                        Key = "fraudDetails",
                        LabelBn = "প্রতারণা বা মিথ্যা তথ্যের প্রকৃতি",
                        LabelEn = "Details of Fraud / Misrepresentation",
                        Order = 5,
                        IsRequired = false,
                        FieldType = "Text",
                        SourceQuestionCodes = ["fraudDetails", "FRAUD_DETAILS"]
                    },
                    new()
                    {
                        Key = "financialLoss",
                        LabelBn = "আর্থিক বা মানসিক ক্ষতির বিবরণ",
                        LabelEn = "Financial or Emotional Loss",
                        Order = 6,
                        IsRequired = false,
                        FieldType = "Amount",
                        SourceQuestionCodes = ["financialLoss", "claimedAmount"]
                    },
                    new()
                    {
                        Key = "evidenceList",
                        LabelBn = "প্রমাণাদি ও প্রাসঙ্গিক নথিপত্র",
                        LabelEn = "Evidence & Documents",
                        Order = 7,
                        IsRequired = false,
                        FieldType = "EvidenceList",
                        SourceQuestionCodes = ["evidenceList", "EVIDENCE_CHECK"]
                    },
                    new()
                    {
                        Key = "desiredRelief",
                        LabelBn = "প্রার্থিত আইনি সহায়তা বা প্রতিকার",
                        LabelEn = "Desired Legal Advice / Relief",
                        Order = 8,
                        IsRequired = true,
                        FieldType = "Choice",
                        SourceQuestionCodes = ["desiredRelief", "RELIEF_PRAYER"]
                    }
                ]
            }
        };

        foreach (var tmpl in templates)
        {
            var existing = await _context.CaseSummaryTemplates
                .Find(x => x.ParentCategoryCode == tmpl.ParentCategoryCode)
                .FirstOrDefaultAsync(ct);

            if (existing == null)
            {
                tmpl.CreatedAt = DateTime.UtcNow;
                tmpl.UpdatedAt = DateTime.UtcNow;
                await _context.CaseSummaryTemplates.InsertOneAsync(tmpl, cancellationToken: ct);
            }
            else
            {
                var update = Builders<CaseSummaryTemplateModel>.Update
                    .Set(x => x.Name, tmpl.Name)
                    .Set(x => x.NameEn, tmpl.NameEn)
                    .Set(x => x.Description, tmpl.Description)
                    .Set(x => x.Version, tmpl.Version)
                    .Set(x => x.IsActive, tmpl.IsActive)
                    .Set(x => x.Fields, tmpl.Fields)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);

                await _context.CaseSummaryTemplates.UpdateOneAsync(
                    x => x.ParentCategoryCode == tmpl.ParentCategoryCode,
                    update,
                    cancellationToken: ct);
            }
        }

        _logger.LogInformation("Seeded/Verified {Count} case summary templates.", templates.Count);
    }

    private async Task SeedApplicationTemplatesAsync(CancellationToken ct)
    {
        var templates = new List<ApplicationTemplateModel>
        {
            // 1. DOWER_MAINTENANCE
            new()
            {
                CategoryCode = "DOWER_MAINTENANCE",
                TitleBn = "দেনমোহর ও খোরপোষ আদায়ের পারিবারিক আদালত আবেদন",
                TitleEn = "Family Court Application for Realization of Dower & Maintenance",
                RecipientAuthorityBn = "বিজ্ঞ সহকারী জজ ও পারিবারিক আদালত, {{DISTRICT}}",
                SubjectBn = "দেনমোহর ও বকেয়া খোরপোষের টাকা আদায়ের মোকদ্দমা",
                BodyTemplateBn = """
                    বিজ্ঞ সহকারী জজ ও পারিবারিক আদালত, {{DISTRICT}}
                    
                    বিষয়: পারিবারিক আদালত আইন, ২০২৩ অনুযায়ী দেনমোহর ও খোরপোষ আদায়ের আবেদন।
                    
                    আবেদনকারী:
                    {{APPLICANT_NAME}}
                    ঠিকানা: {{APPLICANT_ADDRESS}}
                    
                    বনাম
                    
                    বিবাদী:
                    {{RESPONDENT_NAME}}
                    ঠিকানা: {{RESPONDENT_ADDRESS}}
                    
                    মাননীয় আদালতের নিকট আবেদনকারীর বিনীত নিবেদন এই যে,
                    
                    ১। আবেদনকারী ও বিবাদীর মধ্যে বিগত {{MARRIAGE_DATE}} তারিখে ইসলামি শরিয়ত মোতাবেক ও মুসলিম বিবাহ ও তালাক (নিবন্ধন) আইন অনুযায়ী শুভ বিবাহ সম্পন্ন হয়।
                    
                    ২। উক্ত বিবাহে দেনমোহর বাবদ সর্বমোট {{DOWER_TOTAL_AMOUNT}} টাকা ধার্য করা হয়। যার মধ্যে উসূল বাবদ {{DOWER_PAID_AMOUNT}} টাকা ব্যতীত অবশিষ্ট বকেয়া দেনমোহর বাবদ {{DOWER_UNPAID_AMOUNT}} টাকা বিবাদী পরিশোধ করেননি।
                    
                    ৩। বিবাদী বিগত বেশ কিছুদিন যাবৎ আবেদনকারীকে কোনো প্রকার খোরপোষ প্রদান না করায় বকেয়া খোরপোষ বাবদ {{MAINTENANCE_DUE}} টাকা পাওনা রয়েছে।
                    
                    ৪। পারিবারিক প্রেক্ষাপট ও ঘটনার সংক্ষিপ্ত বিবরণ:
                    {{INCIDENT_DETAILS}}
                    
                    ৫। দাবির সমর্থনে প্রমাণাদির তালিকা:
                    {{EVIDENCE_LIST}}
                    
                    প্রার্থনা:
                    অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত অনুগ্রহপূর্বক বিবাদীর নিকট হইতে আবেদনকারীর দেনমোহর ও বকেয়া খোরপোষ বাবদ সমুদয় অর্থ আদায়ের আদেশদানে এবং উপযুক্ত আইনানুগ প্রতিকার প্রদানে মর্জি হয়।
                    
                    তারিখ: {{APPLICATION_DATE}}
                    নিবেদক,
                    {{APPLICANT_NAME}}
                    """,
                SupportedPlaceholders =
                [
                    new() { Placeholder = "{{APPLICANT_NAME}}", Key = "APPLICANT_NAME", LabelBn = "আবেদনকারীর নাম", IsRequired = true },
                    new() { Placeholder = "{{RESPONDENT_NAME}}", Key = "RESPONDENT_NAME", LabelBn = "বিবাদীর নাম", IsRequired = true },
                    new() { Placeholder = "{{MARRIAGE_DATE}}", Key = "MARRIAGE_DATE", LabelBn = "বিবাহের তারিখ", IsRequired = true },
                    new() { Placeholder = "{{DOWER_TOTAL_AMOUNT}}", Key = "DOWER_TOTAL_AMOUNT", LabelBn = "মোট দেনমোহর", IsRequired = true },
                    new() { Placeholder = "{{DOWER_PAID_AMOUNT}}", Key = "DOWER_PAID_AMOUNT", LabelBn = "পরিশোধিত দেনমোহর", IsRequired = false },
                    new() { Placeholder = "{{DOWER_UNPAID_AMOUNT}}", Key = "DOWER_UNPAID_AMOUNT", LabelBn = "বকেয়া দেনমোহর", IsRequired = true },
                    new() { Placeholder = "{{MAINTENANCE_DUE}}", Key = "MAINTENANCE_DUE", LabelBn = "বকেয়া খোরপোষ", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_DETAILS}}", Key = "INCIDENT_DETAILS", LabelBn = "ঘটনার বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{EVIDENCE_LIST}}", Key = "EVIDENCE_LIST", LabelBn = "প্রমাণাদির তালিকা", IsRequired = false },
                    new() { Placeholder = "{{DISTRICT}}", Key = "DISTRICT", LabelBn = "জেলা", IsRequired = false }
                ],
                Version = 1,
                IsActive = true
            },

            // 2. DOWRY_DOMESTIC_VIOLENCE
            new()
            {
                CategoryCode = "DOWRY_DOMESTIC_VIOLENCE",
                TitleBn = "যৌতুক নিরোধ ও পারিবারিক সহিংসতা সুরক্ষা অভিযোগ",
                TitleEn = "Complaint on Dowry Prohibition & Domestic Violence Protection",
                RecipientAuthorityBn = "বিজ্ঞ চিফ জুডিসিয়াল ম্যাজিস্ট্রেট / মেট্রোপলিটন ম্যাজিস্ট্রেট আদালত, {{DISTRICT}}",
                SubjectBn = "যৌতুক নিরোধ আইন, ২০১৮ এবং পারিবারিক সহিংসতা (প্রতিরোধ ও সুরক্ষা) আইন, ২০১০ অনুযায়ী প্রতিকার পাওয়ার দরখাস্ত",
                BodyTemplateBn = """
                    বিজ্ঞ আদালত, {{DISTRICT}}
                    
                    বিষয়: যৌতুক দাবি ও পারিবারিক নির্যাতনের বিরুদ্ধে আইনানুগ ব্যবস্থা গ্রহণ ও সুরক্ষার আবেদন।
                    
                    অভিযোগকারী:
                    {{APPLICANT_NAME}}
                    
                    বনাম
                    
                    অভিযুক্ত:
                    {{RESPONDENT_NAME}}
                    
                    মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,
                    
                    ১। অভিযোগকারী ও অভিযুক্তের বিবাহের পর হইতে অভিযুক্ত পক্ষ বিভিন্ন সময়ে বেআইনিভাবে {{DOWRY_DEMAND_AMOUNT}} টাকা যৌতুক দাবি করিয়া আসিতেছে।
                    
                    ২। উক্ত যৌতুকের দাবি পূরণ না করায় অভিযোগকারীর ওপর বিভিন্ন সময়ে শারীরিক ও মানসিক নির্যাতন চালানো হয় (নির্যাতনের ধরন: {{ABUSE_TYPE}})।
                    
                    ৩। শারীরিক আঘাত ও জখমের বিবরণ: {{INJURY_DETAILS}}।
                    
                    ৪। ঘটনাস্থল ও উচ্ছেদের বিবরণ: {{INCIDENT_LOCATION}}।
                    
                    ৫। বর্তমান নিরাপত্তা ঝুঁকি ও হুমকি: {{IMMEDIATE_THREAT}}।
                    
                    ৬। ঘটনার বিস্তারিত বিবরণ:
                    {{INCIDENT_DETAILS}}
                    
                    ৭। প্রমাণাদির বিবরণ: {{EVIDENCE_LIST}}।
                    
                    প্রার্থনা:
                    অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত অভিযোগ আমলে নিয়ে অভিযুক্তের বিরুদ্ধে যৌতুক নিরোধ আইন অনুযায়ী আইনানুগ ব্যবস্থা গ্রহণ এবং অভিযোগকারীর শারীরিক নিরাপত্তা সুরক্ষায় সুরক্ষা আদেশ প্রদানে মর্জি হয়।
                    
                    তারিখ: {{APPLICATION_DATE}}
                    নিবেদক,
                    {{APPLICANT_NAME}}
                    """,
                SupportedPlaceholders =
                [
                    new() { Placeholder = "{{APPLICANT_NAME}}", Key = "APPLICANT_NAME", LabelBn = "অভিযোগকারীর নাম", IsRequired = true },
                    new() { Placeholder = "{{RESPONDENT_NAME}}", Key = "RESPONDENT_NAME", LabelBn = "অভিযুক্তের নাম", IsRequired = true },
                    new() { Placeholder = "{{DOWRY_DEMAND_AMOUNT}}", Key = "DOWRY_DEMAND_AMOUNT", LabelBn = "দাবিকৃত যৌতুক", IsRequired = true },
                    new() { Placeholder = "{{ABUSE_TYPE}}", Key = "ABUSE_TYPE", LabelBn = "নির্যাতনের ধরন", IsRequired = true },
                    new() { Placeholder = "{{INJURY_DETAILS}}", Key = "INJURY_DETAILS", LabelBn = "আঘাতের বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_LOCATION}}", Key = "INCIDENT_LOCATION", LabelBn = "ঘটনাস্থল", IsRequired = false },
                    new() { Placeholder = "{{IMMEDIATE_THREAT}}", Key = "IMMEDIATE_THREAT", LabelBn = "নিরাপত্তা ঝুঁকি", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_DETAILS}}", Key = "INCIDENT_DETAILS", LabelBn = "ঘটনার বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{EVIDENCE_LIST}}", Key = "EVIDENCE_LIST", LabelBn = "প্রমাণাদির তালিকা", IsRequired = false },
                    new() { Placeholder = "{{DISTRICT}}", Key = "DISTRICT", LabelBn = "জেলা", IsRequired = false }
                ],
                Version = 1,
                IsActive = true
            },

            // 3. LABOUR_WAGE
            new()
            {
                CategoryCode = "LABOUR_WAGE",
                TitleBn = "বকেয়া মজুরি ও শ্রম পাওনা আদায়ের আবেদন",
                TitleEn = "Claim Application for Recovery of Unpaid Wages & Benefits",
                RecipientAuthorityBn = "বিজ্ঞ চেয়ারম্যান, শ্রম আদালত / উপ-মহাপরিদর্শক, কলকারখানা ও প্রতিষ্ঠান পরিদর্শন অধিদপ্তর, {{DISTRICT}}",
                SubjectBn = "বাংলাদেশ শ্রম আইন, ২০০৬ এর ৩৩ ধারা অনুযায়ী বকেয়া মজুরি ও আইনানুগ পাওনা আদায়ের আবেদন",
                BodyTemplateBn = """
                    বিজ্ঞ শ্রম আদালত / শ্রম কর্মকর্তা, {{DISTRICT}}
                    
                    বিষয়: বকেয়া বেতন-ভাতা ও শ্রম ক্ষতিপূরণ আদায়ের দরখাস্ত।
                    
                    আবেদনকারী (শ্রমিক):
                    {{APPLICANT_NAME}}
                    পদবী: {{JOB_DESIGNATION}}
                    
                    বনাম
                    
                    বিবাদী (মালিক/প্রতিষ্ঠান):
                    {{EMPLOYER_NAME}}
                    
                    মাননীয় কর্তৃপক্ষের নিকট বিনীত নিবেদন এই যে,
                    
                    ১। আবেদনকারী বিবাদীর প্রতিষ্ঠানে {{JOB_DESIGNATION}} হিসেবে মাসিক {{MONTHLY_SALARY}} টাকা বেতনে নিষ্ঠার সহিত দায়িত্ব পালন করিয়া আসিতেছিলেন।
                    
                    ২। বিবাদী প্রতিষ্ঠান বিগত {{UNPAID_MONTHS}} মাসের বেতন বাবদ সর্বমোট {{UNPAID_WAGE_AMOUNT}} টাকা এবং ওভারটাইম/ভাতা বাবদ {{OVERTIME_DUE}} টাকা অদ্যাবধি পরিশোধ না করিয়া অন্যায়ভাবে বকেয়া রাখিয়াছে।
                    
                    ৩। ঘটনার বিস্তারিত বিবরণ ও বরখাস্তের প্রেক্ষাপট:
                    {{INCIDENT_DETAILS}}
                    
                    ৪। নথিপত্র ও প্রমাণাদি: {{EVIDENCE_LIST}}।
                    
                    প্রার্থনা:
                    অতএব, বিনীত প্রার্থনা এই যে, আবেদনকারীর আইনানুগ বকেয়া মজুরি ও ক্ষতিপূরণের টাকা আদায় করিয়া দিতে আইনগত আদেশ প্রদানে মর্জি হয়।
                    
                    তারিখ: {{APPLICATION_DATE}}
                    নিবেদক,
                    {{APPLICANT_NAME}}
                    """,
                SupportedPlaceholders =
                [
                    new() { Placeholder = "{{APPLICANT_NAME}}", Key = "APPLICANT_NAME", LabelBn = "শ্রমিকের নাম", IsRequired = true },
                    new() { Placeholder = "{{EMPLOYER_NAME}}", Key = "EMPLOYER_NAME", LabelBn = "মালিক/প্রতিষ্ঠানের নাম", IsRequired = true },
                    new() { Placeholder = "{{JOB_DESIGNATION}}", Key = "JOB_DESIGNATION", LabelBn = "পদবী", IsRequired = false },
                    new() { Placeholder = "{{MONTHLY_SALARY}}", Key = "MONTHLY_SALARY", LabelBn = "মাসিক বেতন", IsRequired = false },
                    new() { Placeholder = "{{UNPAID_WAGE_AMOUNT}}", Key = "UNPAID_WAGE_AMOUNT", LabelBn = "বকেয়া মজুরির পরিমাণ", IsRequired = true },
                    new() { Placeholder = "{{UNPAID_MONTHS}}", Key = "UNPAID_MONTHS", LabelBn = "বকেয়া মাস", IsRequired = false },
                    new() { Placeholder = "{{OVERTIME_DUE}}", Key = "OVERTIME_DUE", LabelBn = "ওভারটাইম পাওনা", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_DETAILS}}", Key = "INCIDENT_DETAILS", LabelBn = "ঘটনার বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{EVIDENCE_LIST}}", Key = "EVIDENCE_LIST", LabelBn = "প্রমাণাদির তালিকা", IsRequired = false },
                    new() { Placeholder = "{{DISTRICT}}", Key = "DISTRICT", LabelBn = "জেলা", IsRequired = false }
                ],
                Version = 1,
                IsActive = true
            },

            // 4. LAND_PROPERTY
            new()
            {
                CategoryCode = "LAND_PROPERTY",
                TitleBn = "জমি ও সম্পত্তি বিরোধ নিরসন ও দখল রক্ষার দরখাস্ত",
                TitleEn = "Petition for Resolution of Land Dispute & Protection of Possession",
                RecipientAuthorityBn = "বিজ্ঞ অতিরিক্ত জেলা ম্যাজিস্ট্রেট আদালত / বিজ্ঞ সহকারী জজ আদালত, {{DISTRICT}}",
                SubjectBn = "ফৌজদারী কার্যবিধি ১৪৪/১৪৫ ধারা অনুযায়ী বিরোধপূর্ণ জমিতে শান্তিশৃঙ্খলা বজায় রাখা ও দখল রক্ষার দরখাস্ত",
                BodyTemplateBn = """
                    বিজ্ঞ আদালত, {{DISTRICT}}
                    
                    বিষয়: জমিতে অবৈধ অনুপ্রবেশ, জবরদখল চেষ্টা প্রতিরোধ ও প্রতিকারের আবেদন।
                    
                    প্রথম পক্ষ (আবেদনকারী):
                    {{APPLICANT_NAME}}
                    
                    বনাম
                    
                    দ্বিতীয় পক্ষ (বিবাদী):
                    {{RESPONDENT_NAME}}
                    
                    মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,
                    
                    ১। তফসিল বর্ণিত বিরোধপূর্ণ জমির বিবরণ: {{PROPERTY_DESCRIPTION}}। উক্ত জমির মালিকানা ও স্বত্বাধিকার: {{OWNERSHIP_TYPE}}।
                    
                    ২। বিরোধের প্রকৃতি: {{DISPUTE_NATURE}}।
                    
                    ৩। ক্ষতির বিবরণ ও সম্ভাব্য আর্থিক ক্ষতি: {{FINANCIAL_LOSS}}।
                    
                    ৪। ঘটনার প্রেক্ষাপট:
                    {{INCIDENT_DETAILS}}
                    
                    ৫। জমির খতিয়ান, দলিল ও প্রমাণাদির তালিকা:
                    {{EVIDENCE_LIST}}
                    
                    প্রার্থনা:
                    অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত তফসিল বর্ণিত জমিতে দ্বিতীয় পক্ষের বেআইনি হস্তক্ষেপ বন্ধে এবং শান্তিশৃঙ্খলা বজায় রাখিতে আইনানুগ আদেশ প্রদানে মর্জি হয়।
                    
                    তারিখ: {{APPLICATION_DATE}}
                    নিবেদক,
                    {{APPLICANT_NAME}}
                    """,
                SupportedPlaceholders =
                [
                    new() { Placeholder = "{{APPLICANT_NAME}}", Key = "APPLICANT_NAME", LabelBn = "আবেদনকারীর নাম", IsRequired = true },
                    new() { Placeholder = "{{RESPONDENT_NAME}}", Key = "RESPONDENT_NAME", LabelBn = "বিবাদীর নাম", IsRequired = true },
                    new() { Placeholder = "{{PROPERTY_DESCRIPTION}}", Key = "PROPERTY_DESCRIPTION", LabelBn = "জমির বিবরণ", IsRequired = true },
                    new() { Placeholder = "{{OWNERSHIP_TYPE}}", Key = "OWNERSHIP_TYPE", LabelBn = "মালিকানার ধরন", IsRequired = false },
                    new() { Placeholder = "{{DISPUTE_NATURE}}", Key = "DISPUTE_NATURE", LabelBn = "বিরোধের প্রকৃতি", IsRequired = true },
                    new() { Placeholder = "{{FINANCIAL_LOSS}}", Key = "FINANCIAL_LOSS", LabelBn = "ক্ষতির পরিমাণ", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_DETAILS}}", Key = "INCIDENT_DETAILS", LabelBn = "ঘটনার বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{EVIDENCE_LIST}}", Key = "EVIDENCE_LIST", LabelBn = "প্রমাণাদির তালিকা", IsRequired = false },
                    new() { Placeholder = "{{DISTRICT}}", Key = "DISTRICT", LabelBn = "জেলা", IsRequired = false }
                ],
                Version = 1,
                IsActive = true
            },

            // 5. WOMEN_CHILD_PROTECTION
            new()
            {
                CategoryCode = "WOMEN_CHILD_PROTECTION",
                TitleBn = "নারী ও শিশু সুরক্ষা ও অধিকার সংরক্ষণ আবেদন",
                TitleEn = "Petition for Protection & Custody of Women and Children",
                RecipientAuthorityBn = "বিজ্ঞ নারী ও শিশু নির্যাতন দমন ট্রাইব্যুনাল / বিজ্ঞ পারিবারিক আদালত, {{DISTRICT}}",
                SubjectBn = "নারী ও শিশুর সুরক্ষা, আইনানুগ অভিভাবকত্ব এবং খোরপোষ নিশ্চিতকরণের দরখাস্ত",
                BodyTemplateBn = """
                    বিজ্ঞ আদালত, {{DISTRICT}}
                    
                    বিষয়: নারী ও শিশুর আইনগত সুরক্ষা ও অধিকার সংরক্ষণের আবেদন।
                    
                    আবেদনকারী:
                    {{APPLICANT_NAME}}
                    
                    বনাম
                    
                    বিবাদী:
                    {{RESPONDENT_NAME}}
                    
                    মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,
                    
                    ১। সন্তানের বিবরণ: {{CHILD_DETAILS}}।
                    
                    ২। বঞ্চনা ও অধিকার লঙ্ঘনের প্রকৃতি: {{DEPRIVATION_NATURE}}।
                    
                    ৩। শিশুর নিরাপত্তা ঝুঁকি ও হুমকি: {{CHILD_SAFETY_RISK}}।
                    
                    ৪। কাস্টডি ও অভিভাবকত্ব বিরোধের বিবরণ: {{CUSTODY_DISPUTE}}।
                    
                    ৫। ঘটনার বিস্তারিত বিবরণ:
                    {{INCIDENT_DETAILS}}
                    
                    ৬। প্রমাণাদি: {{EVIDENCE_LIST}}।
                    
                    প্রার্থনা:
                    অতএব, বিনীত প্রার্থনা এই যে, বিজ্ঞ আদালত আবেদনকারী ও শিশুর সুরক্ষায় উপযুক্ত আইনি আদেশ ও খোরপোষ মঞ্জুর করিতে মর্জি হয়।
                    
                    তারিখ: {{APPLICATION_DATE}}
                    নিবেদক,
                    {{APPLICANT_NAME}}
                    """,
                SupportedPlaceholders =
                [
                    new() { Placeholder = "{{APPLICANT_NAME}}", Key = "APPLICANT_NAME", LabelBn = "আবেদনকারীর নাম", IsRequired = true },
                    new() { Placeholder = "{{RESPONDENT_NAME}}", Key = "RESPONDENT_NAME", LabelBn = "বিবাদীর নাম", IsRequired = true },
                    new() { Placeholder = "{{CHILD_DETAILS}}", Key = "CHILD_DETAILS", LabelBn = "সন্তানের বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{DEPRIVATION_NATURE}}", Key = "DEPRIVATION_NATURE", LabelBn = "বঞ্চনার প্রকৃতি", IsRequired = true },
                    new() { Placeholder = "{{CHILD_SAFETY_RISK}}", Key = "CHILD_SAFETY_RISK", LabelBn = "নিরাপত্তা ঝুঁকি", IsRequired = false },
                    new() { Placeholder = "{{CUSTODY_DISPUTE}}", Key = "CUSTODY_DISPUTE", LabelBn = "অভিভাবকত্ব বিরোধ", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_DETAILS}}", Key = "INCIDENT_DETAILS", LabelBn = "ঘটনার বিবরণ", IsRequired = false },
                    new() { Placeholder = "{{EVIDENCE_LIST}}", Key = "EVIDENCE_LIST", LabelBn = "প্রমাণাদির তালিকা", IsRequired = false },
                    new() { Placeholder = "{{DISTRICT}}", Key = "DISTRICT", LabelBn = "জেলা", IsRequired = false }
                ],
                Version = 1,
                IsActive = true
            },

            // 6. OTHER
            new()
            {
                CategoryCode = "OTHER",
                TitleBn = "সাধারণ আইনি সহায়তা ও পরামর্শের আবেদন",
                TitleEn = "General Legal Aid & Consultation Application",
                RecipientAuthorityBn = "জেলা লিগ্যাল এইড অফিসার, জেলা লিগ্যাল এইড অফিস, {{DISTRICT}}",
                SubjectBn = "জাতীয় আইনগত সহায়তা প্রদান আইন, ২০০০ অনুযায়ী সরকারি আইনি সহায়তার আবেদন",
                BodyTemplateBn = """
                    জেলা লিগ্যাল এইড অফিসার, জেলা লিগ্যাল এইড অফিস, {{DISTRICT}}
                    
                    বিষয়: আইনি পরামর্শ ও সহায়তার দরখাস্ত।
                    
                    আবেদনকারী:
                    {{APPLICANT_NAME}}
                    
                    বনাম
                    
                    অপর পক্ষ / বিবাদী:
                    {{RESPONDENT_NAME}}
                    
                    মহোদয়,
                    
                    বিনীত নিবেদন এই যে, আবেদনকারী নিম্নবর্ণিত আইনি সমস্যায় পতিত হইয়াছেন:
                    
                    ১। ঘটনার বিবরণ:
                    {{INCIDENT_DETAILS}}
                    
                    ২। প্রার্থিত আইনি সমাধান ও প্রতিকার:
                    {{DESIRED_RELIEF}}
                    
                    ৩। প্রমাণাদির তালিকা:
                    {{EVIDENCE_LIST}}
                    
                    অতএব, বিনীত প্রার্থনা এই যে, আবেদনকারীকে প্রয়োজনীয় আইনি সহায়তা ও সরকারি পরামর্শ প্রদানের ব্যবস্থা গ্রহণে মর্জি হয়।
                    
                    তারিখ: {{APPLICATION_DATE}}
                    নিবেদক,
                    {{APPLICANT_NAME}}
                    """,
                SupportedPlaceholders =
                [
                    new() { Placeholder = "{{APPLICANT_NAME}}", Key = "APPLICANT_NAME", LabelBn = "আবেদনকারীর নাম", IsRequired = true },
                    new() { Placeholder = "{{RESPONDENT_NAME}}", Key = "RESPONDENT_NAME", LabelBn = "অপর পক্ষের নাম", IsRequired = false },
                    new() { Placeholder = "{{INCIDENT_DETAILS}}", Key = "INCIDENT_DETAILS", LabelBn = "ঘটনার বিবরণ", IsRequired = true },
                    new() { Placeholder = "{{DESIRED_RELIEF}}", Key = "DESIRED_RELIEF", LabelBn = "প্রার্থিত প্রতিকার", IsRequired = false },
                    new() { Placeholder = "{{EVIDENCE_LIST}}", Key = "EVIDENCE_LIST", LabelBn = "প্রমাণাদির তালিকা", IsRequired = false },
                    new() { Placeholder = "{{DISTRICT}}", Key = "DISTRICT", LabelBn = "জেলা", IsRequired = false }
                ],
                Version = 1,
                IsActive = true
            }
        };

        foreach (var tmpl in templates)
        {
            var existing = await _context.ApplicationTemplates
                .Find(x => x.CategoryCode == tmpl.CategoryCode)
                .FirstOrDefaultAsync(ct);

            if (existing == null)
            {
                tmpl.CreatedAt = DateTime.UtcNow;
                tmpl.UpdatedAt = DateTime.UtcNow;
                await _context.ApplicationTemplates.InsertOneAsync(tmpl, cancellationToken: ct);
            }
            else
            {
                var update = Builders<ApplicationTemplateModel>.Update
                    .Set(x => x.TitleBn, tmpl.TitleBn)
                    .Set(x => x.TitleEn, tmpl.TitleEn)
                    .Set(x => x.RecipientAuthorityBn, tmpl.RecipientAuthorityBn)
                    .Set(x => x.SubjectBn, tmpl.SubjectBn)
                    .Set(x => x.BodyTemplateBn, tmpl.BodyTemplateBn)
                    .Set(x => x.SupportedPlaceholders, tmpl.SupportedPlaceholders)
                    .Set(x => x.DisclaimerNoticeBn, tmpl.DisclaimerNoticeBn)
                    .Set(x => x.Version, tmpl.Version)
                    .Set(x => x.IsActive, tmpl.IsActive)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);

                await _context.ApplicationTemplates.UpdateOneAsync(
                    x => x.CategoryCode == tmpl.CategoryCode,
                    update,
                    cancellationToken: ct);
            }
        }

        _logger.LogInformation("Seeded/Verified {Count} application templates.", templates.Count);
    }
}
