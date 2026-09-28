using System.Text;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Application.Services;

/// <summary>
/// Deterministic document renderer. This layer NEVER calls an LLM — it fills fixed,
/// pre-approved Bangla legal templates so the generated complaint text is legally
/// reviewed and reproducible. Missing values become highlighted placeholders such as
/// "[বিবাদীর নাম — অপ্রতিষ্ঠিত]" so a paralegal can spot gaps at a glance.
/// </summary>
public sealed class TemplateEngineService : ITemplateEngineService
{
    public string RenderComplaint(CaseModel @case)
    {
        var d = @case.ExtractedData;
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        return @case.Category switch
        {
            LegalCategories.DowerMaintenance => RenderDower(@case, d, today),
            LegalCategories.DowryDomesticViolence or LegalCategories.Dowry => RenderDowry(@case, d, today),
            LegalCategories.LabourWage or LegalCategories.UnpaidWages => RenderUnpaidWages(@case, d, today),
            LegalCategories.LandProperty or LegalCategories.LandDispute => RenderLandDispute(@case, d, today),
            LegalCategories.WomenChildProtection => RenderWomenChildProtection(@case, d, today),
            _ => RenderGeneric(@case, today)
        };
    }

    private static string RenderWomenChildProtection(CaseModel c, IReadOnlyDictionary<string, ExtractedField> d, string today)
    {
        var sb = Header(c, "নারী ও শিশু সুরক্ষা আবেদন", today);
        sb.AppendLine("বিষয়: নারী ও শিশু নির্যাতন দমন আইন / অভিভাবকত্ব ও পারিবারিক সুরক্ষা আইন অনুযায়ী আবেদন।");
        sb.AppendLine();
        sb.AppendLine($"আবেদনকারী: {Ph(d, "applicant", "আবেদনকারীর নাম")}");
        sb.AppendLine($"বিবাদী: {Ph(d, "respondent", "বিবাদীর নাম")}");
        sb.AppendLine();
        sb.AppendLine("মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,");
        sb.AppendLine("১। আবেদনকারী ও নাবালক সন্তানের ওপর পারিবারিক বৈষম্য ও নিরাপত্তা ঝুঁকি সৃষ্টি করা হয়েছে।");
        sb.AppendLine($"২। বিবরণ ও ঘটনার সারাংশ: {Ph(d, "incidentDetails", "ঘটনার বিস্তারিত বিবরণ")}।");
        sb.AppendLine($"৩। সমর্থনকারী প্রমাণাদি: {Ph(d, "evidenceList", "প্রমাণের তালিকা")}।");
        Prayer(sb, "নারী ও শিশুর সুরক্ষা ও আইনানুগ প্রতিকারের");
        return sb.ToString();
    }

    // --- Templates -------------------------------------------------------------

    private static string RenderDower(CaseModel c, IReadOnlyDictionary<string, ExtractedField> d, string today)
    {
        var sb = Header(c, "দেনমোহর ও খোরপোশ আদায়ের দরখাস্ত", today);
        sb.AppendLine($"বিষয়: পারিবারিক আদালত আইন, ১৯৮৫ অনুযায়ী দেনমোহর ও খোরপোশ আদায়ের আবেদন।");
        sb.AppendLine();
        sb.AppendLine($"আবেদনকারী: {Ph(d, "applicant", "আবেদনকারীর নাম")}");
        sb.AppendLine($"বিবাদী: {Ph(d, "respondent", "বিবাদীর নাম")}");
        sb.AppendLine($"বিবাহের তারিখ: {Ph(d, "marriageDate", "বিবাহের তারিখ")}");
        sb.AppendLine();
        sb.AppendLine("মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,");
        sb.AppendLine(
            $"১। আবেদনকারী ও বিবাদীর মধ্যে {Ph(d, "marriageDate", "বিবাহের তারিখ")} তারিখে ধর্মীয় ও " +
            "আইনানুগভাবে বিবাহ সম্পন্ন হয়।");
        sb.AppendLine(
            $"২। কাবিননামা অনুযায়ী নির্ধারিত দেনমোহরের পরিমাণ {Ph(d, "dowerAmount", "দেনমোহরের পরিমাণ")} টাকা, " +
            "যা অদ্যাবধি পরিশোধ করা হয়নি।");
        sb.AppendLine(
            $"৩। আবেদনকারী বিবাদীর নিকট {Ph(d, "claimedAmount", "দাবিকৃত পরিমাণ")} টাকা দেনমোহর ও বকেয়া " +
            "খোরপোশ বাবদ পাওনা রয়েছেন।");
        sb.AppendLine($"৪। সমর্থনকারী প্রমাণাদি: {Ph(d, "evidenceList", "প্রমাণের তালিকা")}।");
        Prayer(sb, "দেনমোহর ও খোরপোশ আদায়ের");
        return sb.ToString();
    }

    private static string RenderDowry(CaseModel c, IReadOnlyDictionary<string, ExtractedField> d, string today)
    {
        var sb = Header(c, "যৌতুক নিরোধ অভিযোগ", today);
        sb.AppendLine("বিষয়: যৌতুক নিরোধ আইন, ২০১৮ অনুযায়ী অভিযোগ দায়ের।");
        sb.AppendLine();
        sb.AppendLine($"অভিযোগকারী: {Ph(d, "applicant", "অভিযোগকারীর নাম")}");
        sb.AppendLine($"অভিযুক্ত: {Ph(d, "respondent", "অভিযুক্তের নাম")}");
        sb.AppendLine($"বিবাহের তারিখ: {Ph(d, "marriageDate", "বিবাহের তারিখ")}");
        sb.AppendLine();
        sb.AppendLine("মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,");
        sb.AppendLine(
            $"১। বিবাহের সময় ও পরবর্তীতে অভিযুক্ত পক্ষ {Ph(d, "claimedAmount", "দাবিকৃত যৌতুকের পরিমাণ")} " +
            "টাকা যৌতুক দাবি করে আসছেন।");
        sb.AppendLine("২। যৌতুক দাবিকে কেন্দ্র করে অভিযোগকারীকে মানসিক ও শারীরিকভাবে হয়রানি করা হচ্ছে।");
        sb.AppendLine($"৩। সমর্থনকারী প্রমাণাদি: {Ph(d, "evidenceList", "প্রমাণের তালিকা")}।");
        Prayer(sb, "যৌতুক দাবির বিরুদ্ধে আইনানুগ ব্যবস্থা গ্রহণের");
        return sb.ToString();
    }

    private static string RenderUnpaidWages(CaseModel c, IReadOnlyDictionary<string, ExtractedField> d, string today)
    {
        var sb = Header(c, "বকেয়া মজুরি আদায়ের অভিযোগ", today);
        sb.AppendLine("বিষয়: শ্রম আইন, ২০০৬ অনুযায়ী বকেয়া মজুরি আদায়ের আবেদন।");
        sb.AppendLine();
        sb.AppendLine($"আবেদনকারী (শ্রমিক): {Ph(d, "applicant", "শ্রমিকের নাম")}");
        sb.AppendLine($"বিবাদী (নিয়োগকর্তা): {Ph(d, "respondent", "নিয়োগকর্তার নাম")}");
        sb.AppendLine();
        sb.AppendLine("মাননীয় কর্তৃপক্ষের নিকট বিনীত নিবেদন এই যে,");
        sb.AppendLine("১। আবেদনকারী বিবাদীর প্রতিষ্ঠানে শ্রমিক হিসেবে নিযুক্ত ছিলেন।");
        sb.AppendLine(
            $"২। আবেদনকারীর নিকট বকেয়া মজুরি বাবদ {Ph(d, "claimedAmount", "বকেয়া মজুরির পরিমাণ")} টাকা পাওনা রয়েছে।");
        sb.AppendLine($"৩। সমর্থনকারী প্রমাণাদি: {Ph(d, "evidenceList", "প্রমাণের তালিকা")}।");
        Prayer(sb, "বকেয়া মজুরি আদায়ের");
        return sb.ToString();
    }

    private static string RenderLandDispute(CaseModel c, IReadOnlyDictionary<string, ExtractedField> d, string today)
    {
        var sb = Header(c, "জমি বিরোধ সংক্রান্ত দরখাস্ত", today);
        sb.AppendLine("বিষয়: ভূমি সংক্রান্ত বিরোধ নিষ্পত্তির আবেদন।");
        sb.AppendLine();
        sb.AppendLine($"আবেদনকারী: {Ph(d, "applicant", "আবেদনকারীর নাম")}");
        sb.AppendLine($"বিবাদী: {Ph(d, "respondent", "বিবাদীর নাম")}");
        sb.AppendLine();
        sb.AppendLine("মাননীয় আদালতের নিকট বিনীত নিবেদন এই যে,");
        sb.AppendLine("১। বিরোধপূর্ণ জমির মালিকানা ও দখল নিয়ে উভয় পক্ষের মধ্যে বিরোধ বিদ্যমান।");
        sb.AppendLine($"২। আবেদনকারীর দাবিকৃত পরিমাণ/মূল্য: {Ph(d, "claimedAmount", "দাবিকৃত পরিমাণ")}।");
        sb.AppendLine($"৩। সমর্থনকারী প্রমাণাদি: {Ph(d, "evidenceList", "দলিল/প্রমাণের তালিকা")}।");
        Prayer(sb, "জমি বিরোধ নিষ্পত্তির");
        return sb.ToString();
    }

    private static string RenderGeneric(CaseModel c, string today)
    {
        var sb = Header(c, "আইনি সহায়তার আবেদন", today);
        sb.AppendLine("এই অভিযোগটির শ্রেণিবিন্যাস এখনও সম্পন্ন হয়নি অথবা এটি সেবার আওতাবহির্ভূত।");
        sb.AppendLine("অনুগ্রহ করে একজন প্যারালিগ্যালের সাথে যোগাযোগ করুন।");
        return sb.ToString();
    }

    // --- Helpers ---------------------------------------------------------------

    private static StringBuilder Header(CaseModel c, string title, string today)
    {
        var sb = new StringBuilder();
        sb.AppendLine("প্রকল্প অধিকার — আইনি সহায়তা খসড়া");
        sb.AppendLine($"মামলা নম্বর: {c.CaseNumber}");
        sb.AppendLine($"তারিখ: {today}");
        sb.AppendLine();
        sb.AppendLine(title);
        sb.AppendLine(new string('-', 40));
        sb.AppendLine();
        return sb;
    }

    private static void Prayer(StringBuilder sb, string relief)
    {
        sb.AppendLine();
        sb.AppendLine($"অতএব, মাননীয় কর্তৃপক্ষের নিকট আবেদনকারীর বিনীত প্রার্থনা এই যে, {relief} " +
                      "প্রয়োজনীয় আইনানুগ ব্যবস্থা গ্রহণ করিতে মর্জি হয়।");
        sb.AppendLine();
        sb.AppendLine("নিবেদক,");
        sb.AppendLine("আবেদনকারীর পক্ষে — প্রকল্প অধিকার প্যারালিগ্যাল সহায়তা");
    }

    /// <summary>Returns the stated value, or a highlighted "unresolved" placeholder when missing.</summary>
    private static string Ph(IReadOnlyDictionary<string, ExtractedField> d, string key, string label)
    {
        if (d.TryGetValue(key, out var field) && !string.IsNullOrWhiteSpace(field.Value))
        {
            return field.IsInferred ? $"{field.Value} [অনুমিত]" : field.Value!;
        }

        return $"[{label} — অপ্রতিষ্ঠিত]";
    }
}
