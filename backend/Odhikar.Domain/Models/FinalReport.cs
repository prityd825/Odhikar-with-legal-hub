using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Chronological timeline item within the final report.
/// </summary>
public class ReportTimelineEntry
{
    [BsonElement("dateOrTime")]
    public string? DateOrTime { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("sourceTurn")]
    [BsonIgnoreIfNull]
    public int? SourceTurn { get; set; }

    [BsonElement("location")]
    [BsonIgnoreIfNull]
    public string? Location { get; set; }
}

/// <summary>
/// Alias for timeline entry for clean report representation.
/// </summary>
public sealed class TimelineItem : ReportTimelineEntry
{
}

/// <summary>
/// Structured assessment of safety risks, emergency alerts, and threats.
/// </summary>
public class ReportSafetySummary
{
    [BsonElement("hasSafetyConcerns")]
    public bool HasSafetyConcerns { get; set; }

    [BsonElement("isEmergencyEscalated")]
    public bool IsEmergencyEscalated { get; set; }

    [BsonElement("safetySummary")]
    public string SafetySummary { get; set; } = "কোনো তাৎক্ষণিক শারীরিক নিরাপত্তা ঝুঁকি বা হুমকি রিপোর্ট করা হয়নি।";

    [BsonElement("riskFactors")]
    public List<string> RiskFactors { get; set; } = [];

    [BsonElement("emergencyHelplineNotified")]
    public bool EmergencyHelplineNotified { get; set; }
}

/// <summary>
/// Alias for safety concerns assessment.
/// </summary>
public sealed class SafetyConcernsInfo : ReportSafetySummary
{
}

/// <summary>
/// Extensible client/applicant profile for legal report generation and PDF drafting.
/// </summary>
public sealed class ReportClientProfile
{
    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("phone")]
    [BsonIgnoreIfNull]
    public string? Phone { get; set; }

    [BsonElement("address")]
    [BsonIgnoreIfNull]
    public string? Address { get; set; }

    [BsonElement("nidOrIdentifier")]
    [BsonIgnoreIfNull]
    public string? NidOrIdentifier { get; set; }
}

/// <summary>
/// Extensible respondent/opposing party profile for legal report generation.
/// </summary>
public sealed class ReportOpposingPartyProfile
{
    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }

    [BsonElement("relation")]
    [BsonIgnoreIfNull]
    public string? Relation { get; set; }

    [BsonElement("address")]
    [BsonIgnoreIfNull]
    public string? Address { get; set; }

    [BsonElement("contactDetails")]
    [BsonIgnoreIfNull]
    public string? ContactDetails { get; set; }
}

/// <summary>
/// Legal assessment, applicable statutes, and recommended remedies under Bangladesh law.
/// </summary>
public sealed class ReportLegalRelief
{
    [BsonElement("primaryCategory")]
    public string PrimaryCategory { get; set; } = "সাধারণ আইনি সমস্যা";

    [BsonElement("applicableStatutes")]
    public List<string> ApplicableStatutes { get; set; } = [];

    [BsonElement("soughtRemedy")]
    [BsonIgnoreIfNull]
    public string? SoughtRemedy { get; set; }

    [BsonElement("disputedAmount")]
    [BsonIgnoreIfNull]
    public string? DisputedAmount { get; set; }

    [BsonElement("legalAssessment")]
    [BsonIgnoreIfNull]
    public string? LegalAssessment { get; set; }

    [BsonElement("recommendedNextSteps")]
    public List<string> RecommendedNextSteps { get; set; } = [];
}

/// <summary>
/// Metadata and export configurations for future PDF generation.
/// </summary>
public sealed class PdfExportMetadata
{
    [BsonElement("isPdfGenerated")]
    public bool IsPdfGenerated { get; set; }

    [BsonElement("pdfUrl")]
    [BsonIgnoreIfNull]
    public string? PdfUrl { get; set; }

    [BsonElement("pdfFileName")]
    [BsonIgnoreIfNull]
    public string? PdfFileName { get; set; }

    [BsonElement("exportedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ExportedAt { get; set; }

    [BsonElement("pageCount")]
    public int PageCount { get; set; } = 1;

    [BsonElement("templateVersion")]
    public string TemplateVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Display configurations for frontend report view and paralegal dashboard.
/// </summary>
public sealed class FrontendDisplaySettings
{
    [BsonElement("accentColor")]
    public string AccentColor { get; set; } = "#1E40AF";

    [BsonElement("statusBadge")]
    public string StatusBadge { get; set; } = "Finalized";

    [BsonElement("showTimeline")]
    public bool ShowTimeline { get; set; } = true;

    [BsonElement("showDisclaimers")]
    public bool ShowDisclaimers { get; set; } = true;

    [BsonElement("language")]
    public string Language { get; set; } = "bn";
}

/// <summary>
/// Structured Final Report model representing the complete legal dossier of a completed intake conversation.
/// Extensible for PDF generation and frontend legal aid dashboards.
/// Persisted in the 'final_reports' and 'conversation_reports' MongoDB collections.
/// </summary>
[BsonIgnoreExtraElements]
public class FinalReport
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Associated conversation session ID.</summary>
    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    /// <summary>Linked Case ID in the 'cases' collection.</summary>
    [BsonElement("caseId")]
    [BsonIgnoreIfNull]
    public string? CaseId { get; set; }

    /// <summary>Official public Case Reference Number (e.g. ODH-2026-0012).</summary>
    [BsonElement("caseNumber")]
    [BsonIgnoreIfNull]
    public string? CaseNumber { get; set; }

    /// <summary>Report headline / title.</summary>
    [BsonElement("title")]
    public string Title { get; set; } = "আইনি অভিযোগ চূড়ান্ত প্রতিবেদন";

    /// <summary>Full executive summary of the dispute and client allegations.</summary>
    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>All legal incident types detected across the conversation (e.g. DOWER_MAINTENANCE, DOWRY).</summary>
    [BsonElement("incidentTypes")]
    public List<string> IncidentTypes { get; set; } = [];

    /// <summary>Important factual allegations, admissions, and events recounted by the user.</summary>
    [BsonElement("importantFacts")]
    public List<string> ImportantFacts { get; set; } = [];

    /// <summary>Chronological timeline of events leading up to the dispute.</summary>
    [BsonElement("timeline")]
    public List<ReportTimelineEntry> Timeline { get; set; } = [];

    /// <summary>Safety evaluation, emergency signals, and threat analysis.</summary>
    [BsonElement("safetyConcerns")]
    public ReportSafetySummary SafetyConcerns { get; set; } = new();

    /// <summary>Critical information, documents, or details still missing.</summary>
    [BsonElement("missingInformation")]
    public List<string> MissingInformation { get; set; } = [];

    /// <summary>Extracted facts key-value dictionary (applicant, respondent, date, amount, location, evidence).</summary>
    [BsonElement("extractedFacts")]
    public Dictionary<string, string> ExtractedFacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Severity rating: Low, Medium, High, Critical.</summary>
    [BsonElement("severity")]
    public string Severity { get; set; } = "Low";

    /// <summary>Severity assessment rationale.</summary>
    [BsonElement("severityReason")]
    [BsonIgnoreIfNull]
    public string? SeverityReason { get; set; }

    /// <summary>Applicant/client profile structured for PDF and UI generation.</summary>
    [BsonElement("clientProfile")]
    public ReportClientProfile ClientProfile { get; set; } = new();

    /// <summary>Opposing party profile structured for legal notice and case drafting.</summary>
    [BsonElement("opposingPartyProfile")]
    public ReportOpposingPartyProfile OpposingPartyProfile { get; set; } = new();

    /// <summary>Legal relief, applicable statutes, and recommendations.</summary>
    [BsonElement("legalRelief")]
    public ReportLegalRelief LegalRelief { get; set; } = new();

    /// <summary>Metadata and export settings for PDF compilation.</summary>
    [BsonElement("pdfExport")]
    public PdfExportMetadata PdfExport { get; set; } = new();

    /// <summary>UI presentation configurations for frontend report view.</summary>
    [BsonElement("frontendDisplay")]
    public FrontendDisplaySettings FrontendDisplay { get; set; } = new();

    /// <summary>Total user dialogue turns completed during the conversation.</summary>
    [BsonElement("totalTurns")]
    public int TotalTurns { get; set; }

    /// <summary>Reason why the intake conversation concluded.</summary>
    [BsonElement("completionReason")]
    [BsonIgnoreIfNull]
    public string? CompletionReason { get; set; }

    /// <summary>Timestamp when the report was generated.</summary>
    [BsonElement("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Raw JSON representation of the LLM generation for auditability.</summary>
    [BsonElement("rawReportJson")]
    [BsonIgnoreIfNull]
    public string? RawReportJson { get; set; }

    /// <summary>Arbitrary extension metadata for future law-firm / NGO integrations.</summary>
    [BsonElement("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    // -------------------------------------------------------------------------
    // Backward and forward compatibility properties
    // -------------------------------------------------------------------------

    [BsonElement("detectedIncidentTypes")]
    [BsonIgnoreIfNull]
    public List<string>? DetectedIncidentTypes
    {
        get => IncidentTypes;
        set { if (value != null) IncidentTypes = value; }
    }

    [BsonElement("importantEvents")]
    [BsonIgnoreIfNull]
    public List<string>? ImportantEvents
    {
        get => ImportantFacts;
        set { if (value != null) ImportantFacts = value; }
    }

    [BsonElement("chronologicalTimeline")]
    [BsonIgnoreIfNull]
    public List<ReportTimelineEntry>? ChronologicalTimeline
    {
        get => Timeline;
        set { if (value != null) Timeline = value; }
    }

    [BsonElement("primaryCategory")]
    [BsonIgnoreIfNull]
    public string? PrimaryCategory
    {
        get => LegalRelief.PrimaryCategory;
        set { if (value != null) LegalRelief.PrimaryCategory = value; }
    }

    [BsonElement("legalAssessment")]
    [BsonIgnoreIfNull]
    public string? LegalAssessment
    {
        get => LegalRelief.LegalAssessment;
        set => LegalRelief.LegalAssessment = value;
    }

    [BsonElement("recommendedNextSteps")]
    [BsonIgnoreIfNull]
    public List<string>? RecommendedNextSteps
    {
        get => LegalRelief.RecommendedNextSteps;
        set { if (value != null) LegalRelief.RecommendedNextSteps = value; }
    }
}

/// <summary>
/// Domain alias for FinalReport ensuring backward-compatibility across services.
/// </summary>
public class FinalConversationReport : FinalReport
{
}
