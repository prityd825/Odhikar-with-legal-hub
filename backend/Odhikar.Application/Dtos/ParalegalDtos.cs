using Odhikar.Domain.Models;

namespace Odhikar.Application.Dtos;

/// <summary>Compact row for the paralegal case list with Bangla category tabs and urgency tags.</summary>
public sealed record CaseListItem
{
    public required string Id { get; init; }
    public required string CaseNumber { get; init; }
    public required string Category { get; init; }
    public string CategoryBn { get; init; } = string.Empty;
    public double CategoryConfidence { get; init; }
    public required string Status { get; init; }
    public string StatusBn { get; init; } = string.Empty;
    public string CategoryTab { get; init; } = "new"; // "urgent" | "new" | "reassign_slot"
    public string CategoryTabBn { get; init; } = "নতুন"; // "জরুরি" | "নতুন" | "স্লট পুনর্নির্ধারণ"
    public bool IsSafetyEscalated { get; init; }
    public string? SafetyReason { get; init; }
    public string? Applicant { get; init; }
    public string? Respondent { get; init; }
    public string? District { get; init; }
    public string? DraftComplaintText { get; init; }
    public string? LatestTranscript { get; init; }
    public string? AudioUrl { get; init; }
    public ClinicReferral? ClinicReferral { get; init; }
    public string? ScheduledSlotBn { get; init; }
    public int DialogueTurnsCount { get; init; }
    public Dictionary<string, ExtractedField> ExtractedData { get; init; } = new();
    public List<DialogueTurn> DialogueHistory { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static CaseListItem From(CaseModel c)
    {
        var isApproved = c.Status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) || c.Status.Equals("RESOLVED", StringComparison.OrdinalIgnoreCase);
        var hasSlot = c.ClinicReferral != null && c.ClinicReferral.ScheduledSlot > DateTime.MinValue;
        var isReassignedOrSlot = hasSlot || c.Status.Equals("REASSIGN_SLOT", StringComparison.OrdinalIgnoreCase) || c.Status.Equals("ASSIGNED", StringComparison.OrdinalIgnoreCase);
        var isUrgent = c.IsSafetyEscalated || c.Status.Equals("URGENT", StringComparison.OrdinalIgnoreCase);

        var tab = isApproved ? "approved" : (isReassignedOrSlot ? "reassign_slot" : (isUrgent ? "urgent" : "new"));
        var tabBn = isApproved ? "অনুমোদিত" : (isReassignedOrSlot ? "স্লট পুনর্নির্ধারণ" : (isUrgent ? "জরুরি" : "নতুন"));

        var statusBn = isUrgent ? "জরুরি পদক্ষেপ প্রয়োজন"
            : (hasSlot ? "স্লট নির্ধারিত"
            : (c.Status.Equals("REASSIGN_SLOT", StringComparison.OrdinalIgnoreCase) ? "স্লট পরিবর্তনের অপেক্ষায়"
            : (c.Status.Equals("ASSIGNED", StringComparison.OrdinalIgnoreCase) ? "স্লট নির্ধারিত"
            : (c.Status.Equals("RESOLVED", StringComparison.OrdinalIgnoreCase) ? "নিষ্পত্তিকৃত" : "নতুন মামলা"))));

        var categoryBn = MapCategoryToBangla(c.Category);
        var slotBn = c.ClinicReferral != null && c.ClinicReferral.ScheduledSlot > DateTime.MinValue
            ? FormatBanglaDateTime(c.ClinicReferral.ScheduledSlot)
            : null;

        return new CaseListItem
        {
            Id = c.Id.ToString(),
            CaseNumber = c.CaseNumber,
            Category = c.Category,
            CategoryBn = categoryBn,
            CategoryConfidence = c.CategoryConfidence,
            Status = c.Status,
            StatusBn = statusBn,
            CategoryTab = tab,
            CategoryTabBn = tabBn,
            IsSafetyEscalated = c.IsSafetyEscalated,
            SafetyReason = c.SafetyReason,
            Applicant = ResolveExtractedValue(c.ExtractedData, "applicant", "applicantName", "motherName", "workerName", "victim", "clientName"),
            Respondent = ResolveExtractedValue(c.ExtractedData, "respondent", "respondentName", "husbandName", "employerName", "opposingParty", "accused"),
            District = ResolveExtractedValue(c.ExtractedData, "district", "location", "incidentLocation", "address"),
            DraftComplaintText = c.DraftComplaintText,
            LatestTranscript = c.DialogueHistory.LastOrDefault()?.Transcript,
            AudioUrl = c.DialogueHistory.LastOrDefault(turn => !string.IsNullOrWhiteSpace(turn.AudioUrl))?.AudioUrl,
            ClinicReferral = c.ClinicReferral,
            ScheduledSlotBn = slotBn,
            DialogueTurnsCount = c.DialogueHistory.Count,
            ExtractedData = c.ExtractedData ?? new(),
            DialogueHistory = c.DialogueHistory ?? new(),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    private static string MapCategoryToBangla(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "সাধারণ আইনি সমস্যা";
        var upper = category.ToUpperInvariant();
        if (upper.Contains("DOWER") || upper.Contains("MAINTENANCE") || upper.Contains("দেনমোহর")) return "দেনমোহর ও ভরণপোষণ";
        if (upper.Contains("DOWRY") || upper.Contains("VIOLENCE") || upper.Contains("যৌতুক")) return "যৌতুক ও পারিবারিক সহিংসতা";
        if (upper.Contains("WAGE") || upper.Contains("LABOUR") || upper.Contains("মজুরি")) return "বকেয়া মজুরি ও শ্রম বিরোধ";
        if (upper.Contains("LAND") || upper.Contains("PROPERTY") || upper.Contains("জমি")) return "জমি ও সম্পত্তি বিরোধ";
        if (upper.Contains("WOMEN") || upper.Contains("CHILD") || upper.Contains("সুরক্ষা")) return "নারী ও শিশু সুরক্ষা";
        return "অন্যান্য আইনি সমস্যা";
    }

    private static string FormatBanglaDateTime(DateTime dt)
    {
        var monthsBn = new[] { "", "জানুয়ারি", "ফেব্রুয়ারি", "মার্চ", "এপ্রিল", "মে", "জুন", "জুলাই", "আগস্ট", "সেপ্টেম্বর", "অক্টোবর", "নভেম্বর", "ডিসেম্বর" };
        var monthName = dt.Month <= 12 ? monthsBn[dt.Month] : dt.Month.ToString();
        var hour = dt.Hour % 12 == 0 ? 12 : dt.Hour % 12;
        var period = dt.Hour < 12 ? "সকাল" : (dt.Hour < 16 ? "দুপুর" : (dt.Hour < 19 ? "বিকাল" : "রাত"));
        var timeStr = $"{period} {hour:D2}:{dt.Minute:D2}";
        var raw = $"{dt.Day:D2} {monthName} {dt.Year}, {timeStr}";

        return raw.Replace('0', '০')
                  .Replace('1', '১')
                  .Replace('2', '২')
                  .Replace('3', '৩')
                  .Replace('4', '৪')
                  .Replace('5', '৫')
                  .Replace('6', '৬')
                  .Replace('7', '৭')
                  .Replace('8', '৮')
                  .Replace('9', '৯');
    }

    private static string? ResolveExtractedValue(Dictionary<string, ExtractedField>? data, params string[] keys)
    {
        if (data is null || data.Count == 0) return null;
        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var direct) && !string.IsNullOrWhiteSpace(direct.Value) && direct.Value != "তথ্য পাওয়া যায়নি" && direct.Value != "জানা নেই")
                return direct.Value.Trim();
            var match = data.FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match.Value != null && !string.IsNullOrWhiteSpace(match.Value.Value) && match.Value.Value != "তথ্য পাওয়া যায়নি" && match.Value.Value != "জানা নেই")
                return match.Value.Value.Trim();
        }
        return null;
    }
}

/// <summary>Request to persist a paralegal-edited draft complaint.</summary>
public sealed record UpdateDraftRequest(string DraftText);

/// <summary>Request to persist paralegal-edited case summary structured fields and draft text.</summary>
public sealed record UpdateCaseSummaryRequest(
    Dictionary<string, string>? Fields,
    string? DraftText
);

/// <summary>Request to reassign or schedule a clinic slot.</summary>
public sealed record ReassignSlotRequest(string ClinicId, DateTime ScheduledSlot);

/// <summary>Clinic with Bangla-formatted slots for scheduling.</summary>
public sealed record ClinicDto(
    string Id,
    string Name,
    string District,
    string Address,
    List<DateTime> AvailableSlots,
    List<string> AvailableSlotsBn
);
