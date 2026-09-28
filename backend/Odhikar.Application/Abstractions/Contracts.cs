using Odhikar.Domain.Models;

namespace Odhikar.Application.Abstractions;

/// <summary>Result of a safety scan over a piece of client input.</summary>
public sealed record SafetyResult(bool IsTriggered, string? Reason, string? MatchedSignal)
{
    public static SafetyResult Safe() => new(false, null, null);
}

/// <summary>
/// Normalised output of the NLU extraction step. This is the single contract the
/// controllers depend on; provider details (Groq vs Gemini) stay hidden.
/// </summary>
public sealed class ExtractionResult
{
    public string Category { get; set; } = LegalCategories.Unknown;
    public double CategoryConfidence { get; set; }

    /// <summary>Extracted facts keyed by field name, each with its own inferred flag.</summary>
    public Dictionary<string, ExtractedField> Fields { get; set; } = new();

    /// <summary>
    /// The single most natural next Bangla question to ask, or null when the LLM
    /// judged that enough information has been gathered.
    /// </summary>
    public string? NextQuestion { get; set; }

    /// <summary>True when the model still needs more info before it can classify/draft confidently.</summary>
    public bool NeedsMoreInfo { get; set; }

    /// <summary>Short plain-Bangla narrative summary of what was understood this turn.</summary>
    public string? NarrativeSummary { get; set; }
}

/// <summary>Low-level chat-completion abstraction. Implemented per LLM vendor.</summary>
public interface ILlmProvider
{
    /// <summary>Vendor key, e.g. "Groq" or "Gemini", used for provider selection.</summary>
    string Name { get; }

    /// <summary>
    /// Runs a JSON-only completion. Implementations must instruct the model to
    /// return a single JSON object and should strip any code fences before returning.
    /// </summary>
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}

/// <summary>
/// Abstraction for resolving active LLM provider based on configuration.
/// </summary>
public interface ILlmProviderResolver
{
    ILlmProvider Resolve();
}


public interface ISafetyGuardrailService
{
    Task<SafetyResult> ScanAsync(string text, CancellationToken ct = default);
}

public interface ISpeechToTextService
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName, string contentType, CancellationToken ct = default);
}

public interface ITextToSpeechService
{
    /// <summary>Synthesises Bangla speech and returns MP3 bytes (empty array if TTS is unconfigured).</summary>
    Task<byte[]> SynthesizeAsync(string text, CancellationToken ct = default);
}

public interface IAiExtractionService
{
    Task<ExtractionResult> ExtractAsync(
        string transcript,
        IReadOnlyList<DialogueTurn> history,
        IReadOnlyDictionary<string, ExtractedField> knownFields,
        CancellationToken ct = default);
}

public interface ITemplateEngineService
{
    /// <summary>Renders the pre-approved Bangla complaint text for a case, inserting highlighted placeholders for missing values.</summary>
    string RenderComplaint(CaseModel @case);
}

public sealed class ApplicationPdfDocumentModel
{
    public string CaseNumber { get; set; } = string.Empty;
    public string ParentCategoryCode { get; set; } = "OTHER";
    public string ParentCategoryNameBn { get; set; } = string.Empty;
    public string TemplateTitleBn { get; set; } = string.Empty;
    public string TemplateTitleEn { get; set; } = string.Empty;
    public string RecipientAuthorityBn { get; set; } = string.Empty;
    public string SubjectBn { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string RespondentName { get; set; } = string.Empty;
    public string ApplicationBodyBn { get; set; } = string.Empty;
    public string DisclaimerNoticeBn { get; set; } = string.Empty;
    public bool IsSafetyEscalated { get; set; }
    public string? SafetyReason { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string ReviewStatus { get; set; } = "PENDING_REVIEW";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public IReadOnlyDictionary<string, string> PlaceholdersUsed { get; set; } = new Dictionary<string, string>();
}

public interface IPdfService
{
    /// <summary>Generates a PDF from the (possibly paralegal-edited) complaint text and returns the file bytes.</summary>
    byte[] GenerateComplaintPdf(CaseModel @case, string complaintText);

    /// <summary>Generates a formal legal application PDF styled from the parent-incident category template.</summary>
    byte[] GenerateApplicationPdf(ApplicationPdfDocumentModel model);
}

public interface ICaseService
{
    Task<CaseModel> ProcessTurnAsync(string? caseId, string transcript, string? audioUrl, bool isVoice, CancellationToken ct = default);
    Task<IReadOnlyList<CaseModel>> GetPrioritizedCasesAsync(CancellationToken ct = default);
    Task<CaseModel?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<CaseModel?> UpdateDraftAsync(string id, string draftText, CancellationToken ct = default);
    Task<string> GenerateCaseNumberAsync(CancellationToken ct = default);
    Task<CaseModel?> UpdateStatusAsync(string id, string status, CancellationToken ct = default);
    Task<CaseModel?> ReassignClinicSlotAsync(string id, string clinicId, DateTime scheduledSlot, CancellationToken ct = default);
    Task<IReadOnlyList<ClinicModel>> GetClinicsAsync(string? district = null, CancellationToken ct = default);
    Task UpdateCaseAsync(CaseModel caseModel, CancellationToken ct = default);
}
