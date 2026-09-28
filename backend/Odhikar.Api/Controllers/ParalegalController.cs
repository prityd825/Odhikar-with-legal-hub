using Microsoft.AspNetCore.Mvc;
using Odhikar.Application.Dtos;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;

namespace Odhikar.Api.Controllers;

/// <summary>Paralegal review surface: prioritized case list, detail, draft edit, PDF export, clinic slots & reassignments.</summary>
[ApiController]
[Route("api/paralegal")]
public sealed class ParalegalController(ICaseService cases, IPdfService pdf) : ControllerBase
{
    private readonly ICaseService _cases = cases;
    private readonly IPdfService _pdf = pdf;

    /// <summary>All cases, urgency-first (escalated/URGENT at the top) with Bangla category tabs and status tags.</summary>
    [HttpGet("cases")]
    public async Task<ActionResult<IReadOnlyList<CaseListItem>>> GetCases(CancellationToken ct)
    {
        var cases = await _cases.GetPrioritizedCasesAsync(ct);
        return Ok(cases.Select(CaseListItem.From).ToList());
    }

    /// <summary>Full case detail including dialogue, transcripts, and extracted facts.</summary>
    [HttpGet("cases/{id}")]
    public async Task<IActionResult> GetCase(string id, CancellationToken ct)
    {
        var @case = await _cases.GetByIdAsync(id, ct);
        return @case is null ? NotFound() : Ok(@case);
    }

    /// <summary>Persists a paralegal-edited draft complaint.</summary>
    [HttpPut("cases/{id}/draft")]
    public async Task<IActionResult> UpdateDraft(string id, [FromBody] UpdateDraftRequest request, CancellationToken ct)
    {
        if (request?.DraftText is null)
        {
            return BadRequest(new { message = "খসড়া টেক্সট প্রয়োজন।" });
        }

        var updated = await _cases.UpdateDraftAsync(id, request.DraftText, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Exports the formal legal application draft to a downloadable PDF based on its parent-incident category template.</summary>
    [HttpGet("cases/{id}/export-pdf")]
    public async Task<IActionResult> ExportPdf(
        string id,
        [FromServices] IApplicationTemplateService appTemplateService,
        CancellationToken ct)
    {
        var @case = await _cases.GetByIdAsync(id, ct);
        if (@case is null)
        {
            return NotFound(new { message = $"Case with ID '{id}' was not found." });
        }

        var pdfModel = await appTemplateService.BuildApplicationPdfModelForCaseAsync(@case, ct);
        var bytes = _pdf.GenerateApplicationPdf(pdfModel);

        var safeCaseNum = string.IsNullOrWhiteSpace(@case.CaseNumber) ? id : @case.CaseNumber;
        var filename = $"{pdfModel.ParentCategoryCode}_{safeCaseNum}.pdf";

        return File(bytes, "application/pdf", filename);
    }

    /// <summary>Returns the structured case summary for a case based on its incident category template.</summary>
    [HttpGet("cases/{id}/summary")]
    public async Task<IActionResult> GetCaseSummary(
        string id,
        [FromServices] ICaseSummaryTemplateService templateService,
        CancellationToken ct)
    {
        var @case = await _cases.GetByIdAsync(id, ct);
        if (@case is null) return NotFound(new { message = $"Case with ID '{id}' not found." });

        var summary = await templateService.BuildSummaryFromCaseAsync(@case, ct);
        return summary is null ? NotFound(new { message = "Could not generate summary." }) : Ok(summary);
    }

    /// <summary>
    /// Persists paralegal-edited structured case summary facts and draft text.
    /// Updates the case's ExtractedData and DraftComplaintText in MongoDB, syncs normalized answers,
    /// and returns the newly evaluated structured case summary so that edits persist across refreshes.
    /// </summary>
    [HttpPut("cases/{id}/summary")]
    public async Task<IActionResult> UpdateCaseSummary(
        string id,
        [FromBody] UpdateCaseSummaryRequest request,
        [FromServices] ICaseSummaryTemplateService templateService,
        [FromServices] IConversationAnswerService answerService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var caseGuid))
        {
            return BadRequest(new { message = "অবৈধ কেস আইডি।" });
        }

        var @case = await _cases.GetByIdAsync(id, ct);
        if (@case is null)
        {
            return NotFound(new { message = $"কেস ফাইল (ID: '{id}') পাওয়া যায়নি।" });
        }

        // 1. Update draft complaint text if provided
        if (request.DraftText is not null)
        {
            @case.DraftComplaintText = request.DraftText;
        }

        // 2. Fetch category template fields for source question codes mapping
        var template = await templateService.GetTemplateByParentCategoryAsync(@case.Category, ct);

        // 3. Update structured ExtractedData and normalize answers
        if (request.Fields is not null && request.Fields.Count > 0)
        {
            foreach (var (key, rawVal) in request.Fields)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;

                var trimmedVal = rawVal?.Trim();
                var isMissing = string.IsNullOrWhiteSpace(trimmedVal)
                    || trimmedVal.Equals("তথ্য পাওয়া যায়নি", StringComparison.OrdinalIgnoreCase)
                    || trimmedVal.Equals("জানা নেই", StringComparison.OrdinalIgnoreCase);

                var templateField = template?.Fields.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

                if (!isMissing)
                {
                    // Update canonical key as stated (verified by paralegal)
                    @case.ExtractedData[key] = ExtractedField.Stated(trimmedVal!);

                    // If template field has source question codes, update them too so any fallback lookup succeeds
                    if (templateField?.SourceQuestionCodes is not null)
                    {
                        foreach (var srcCode in templateField.SourceQuestionCodes)
                        {
                            @case.ExtractedData[srcCode] = ExtractedField.Stated(trimmedVal!);
                        }
                    }

                    // Save normalized answer for this case
                    await answerService.SaveAnswerAsync(new ConversationAnswerModel
                    {
                        CaseId = caseGuid,
                        QuestionCode = key.ToUpperInvariant(),
                        QuestionText = templateField?.LabelBn ?? key,
                        Answer = trimmedVal!,
                        Source = "ParalegalEntry",
                        Confidence = 1.0,
                        AnsweredAt = DateTime.UtcNow
                    }, ct);
                }
                else
                {
                    // Explicitly cleared or marked missing
                    @case.ExtractedData.Remove(key);
                    if (templateField?.SourceQuestionCodes is not null)
                    {
                        foreach (var srcCode in templateField.SourceQuestionCodes)
                        {
                            @case.ExtractedData.Remove(srcCode);
                        }
                    }
                }
            }
        }

        // 4. Update case in persistence
        await _cases.UpdateCaseAsync(@case, ct);

        // 5. Build fresh summary result reflecting all updated facts
        var updatedSummary = await templateService.BuildSummaryFromCaseAsync(@case, ct);

        return Ok(new
        {
            message = "সারসংক্ষেপের তথ্য সফলভাবে সংরক্ষিত হয়েছে।",
            summary = updatedSummary,
            caseItem = CaseListItem.From(@case)
        });
    }

    /// <summary>Returns all legal-aid clinics and their available appointment slots in Bangla.</summary>
    [HttpGet("clinics")]
    public async Task<ActionResult<IReadOnlyList<ClinicDto>>> GetClinics([FromQuery] string? district, CancellationToken ct)
    {
        var clinics = await _cases.GetClinicsAsync(district, ct);
        var dtos = clinics.Select(c => new ClinicDto(
            Id: c.Id.ToString(),
            Name: c.Name,
            District: c.District,
            Address: c.Address,
            AvailableSlots: c.AvailableSlots,
            AvailableSlotsBn: c.AvailableSlots.Select(FormatBanglaDateTime).ToList()
        )).ToList();

        return Ok(dtos);
    }

    /// <summary>Reassigns or schedules a clinic appointment slot for a case (স্লট পুনর্নির্ধারণ).</summary>
    [HttpPost("cases/{id}/reassign-slot")]
    [HttpPut("cases/{id}/reassign-slot")]
    public async Task<IActionResult> ReassignSlot(
        string id,
        [FromBody] ReassignSlotRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.ClinicId))
        {
            return BadRequest(new { message = "অবৈধ ক্লিনিক আইডি।" });
        }

        var updatedCase = await _cases.ReassignClinicSlotAsync(id, request.ClinicId, request.ScheduledSlot, ct);
        if (updatedCase is null)
        {
            return NotFound(new { message = "কেস ফাইল পাওয়া যায়নি।" });
        }

        return Ok(new
        {
            messageBn = "স্লট সফলভাবে পুনর্নির্ধারণ করা হয়েছে।",
            caseItem = CaseListItem.From(updatedCase),
            scheduledSlotBn = FormatBanglaDateTime(updatedCase.ClinicReferral?.ScheduledSlot ?? DateTime.UtcNow),
            clinicName = updatedCase.ClinicReferral?.ClinicName ?? string.Empty
        });
    }

    /// <summary>Updates the status of a case (e.g. URGENT, NEW, REASSIGN_SLOT, ASSIGNED, RESOLVED).</summary>
    [HttpPut("cases/{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        string id,
        [FromBody] UpdateCaseStatusRequest request,
        CancellationToken ct)
    {
        var updatedCase = await _cases.UpdateStatusAsync(id, request.Status ?? CaseStatuses.New, ct);
        return updatedCase is null ? NotFound() : Ok(CaseListItem.From(updatedCase));
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
}

public sealed record UpdateCaseStatusRequest(string Status);