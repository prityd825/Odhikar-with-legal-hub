using System.Text;
using System.Text.Json;
using Odhikar.Domain.Models;
using Odhikar.Application.Abstractions;
using Odhikar.Infrastructure.ExternalServices.Ai;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// NLU engine. Wraps the active LLM with a strict JSON contract that: classifies
/// the legal category, extracts entities, derives implicit arithmetic, flags
/// inferred fields, and produces exactly one natural next Bangla question when
/// information is still missing.
/// </summary>
public sealed class AiExtractionService(LlmProviderResolver resolver, ILogger<AiExtractionService> logger) : IAiExtractionService
{
    private readonly LlmProviderResolver _resolver = resolver;
    private readonly ILogger<AiExtractionService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ExtractionResult> ExtractAsync(
        string transcript,
        IReadOnlyList<DialogueTurn> history,
        IReadOnlyDictionary<string, ExtractedField> knownFields,
        CancellationToken ct = default)
    {
        var userPrompt = BuildUserPrompt(transcript, history, knownFields);

        try
        {
            var raw = await _resolver.Resolve().CompleteJsonAsync(SystemPrompt, userPrompt, ct);
            return Parse(raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed; returning a graceful clarification fallback.");
            return new ExtractionResult
            {
                Category = LegalCategories.Unknown,
                CategoryConfidence = 0,
                NeedsMoreInfo = true,
                NextQuestion = "দুঃখিত, আমি ঠিকমতো বুঝতে পারিনি। আপনি অনুগ্রহ করে আবার একটু বিস্তারিত বলবেন কী সমস্যা হয়েছে?"
            };
        }
    }

    private const string SystemPrompt =
        """
        You are the NLU engine of "Odhikar", a Bangla legal-aid paralegal assistant for
        low-literacy clients. You MUST reply with a SINGLE JSON object and nothing else.

        Legal categories (choose exactly one for "category"):
          - "DOWER_MAINTENANCE" (দেনমোহর/খোরপোশ)
          - "DOWRY" (যৌতুক)
          - "UNPAID_WAGES" (বকেয়া মজুরি)
          - "LAND_DISPUTE" (জমি বিরোধ)
          - "OUT_OF_SCOPE" (criminal matters, tax, immigration, corporate, or anything not in the four categories above)
          - "UNKNOWN" (cannot yet tell — use ONLY when more information is needed)

        Rules:
          1. Extract these fields when present: applicant, respondent, marriageDate (ISO yyyy-MM-dd if possible),
             dowerAmount, claimedAmount, evidenceList. Use null for anything not stated.
          2. Derive implicit arithmetic. Example: unpaid wages claimedAmount = totalEarned - amountPaid.
             When a value is derived OR guessed rather than explicitly stated, set its "isInferred" to true
             and give a short Bangla "inferenceNote" explaining the derivation.
          3. If information is missing, DO NOT force a low-confidence classification. Instead keep
             "needsMoreInfo": true, set the best-guess category (or "UNKNOWN"), and put exactly ONE
             natural, warm, simple Bangla follow-up question in "nextQuestion".
          4. When the client's matter is clearly not one of the four in-scope categories, set
             category "OUT_OF_SCOPE", needsMoreInfo false, nextQuestion null.
          5. When enough information has been gathered, set needsMoreInfo false and nextQuestion null.
          6. "categoryConfidence" is a number between 0 and 1.
          7. Keep all client-facing text in simple, respectful Bangla.

        Respond with this exact JSON shape:
        {
          "category": "DOWER_MAINTENANCE|DOWRY|UNPAID_WAGES|LAND_DISPUTE|OUT_OF_SCOPE|UNKNOWN",
          "categoryConfidence": 0.0,
          "needsMoreInfo": true,
          "nextQuestion": "single Bangla question or null",
          "narrativeSummary": "one short Bangla sentence summarising what was understood",
          "fields": {
            "applicant":    { "value": "string|null", "isInferred": false, "inferenceNote": null },
            "respondent":   { "value": "string|null", "isInferred": false, "inferenceNote": null },
            "marriageDate": { "value": "string|null", "isInferred": false, "inferenceNote": null },
            "dowerAmount":  { "value": "string|null", "isInferred": false, "inferenceNote": null },
            "claimedAmount":{ "value": "string|null", "isInferred": false, "inferenceNote": null },
            "evidenceList": { "value": "string|null", "isInferred": false, "inferenceNote": null }
          }
        }
        """;

    private static string BuildUserPrompt(
        string transcript,
        IReadOnlyList<DialogueTurn> history,
        IReadOnlyDictionary<string, ExtractedField> knownFields)
    {
        var sb = new StringBuilder();

        if (history.Count > 0)
        {
            sb.AppendLine("Conversation so far (oldest first):");
            foreach (var turn in history)
            {
                if (!string.IsNullOrWhiteSpace(turn.Transcript))
                {
                    sb.AppendLine($"CLIENT: {turn.Transcript}");
                }
                if (!string.IsNullOrWhiteSpace(turn.SystemPrompt))
                {
                    sb.AppendLine($"ASSISTANT: {turn.SystemPrompt}");
                }
            }
            sb.AppendLine();
        }

        if (knownFields.Count > 0)
        {
            sb.AppendLine("Already-known fields (merge with new info, keep existing unless corrected):");
            foreach (var (key, field) in knownFields)
            {
                sb.AppendLine($"- {key}: {field.Value ?? "null"}{(field.IsInferred ? " (inferred)" : string.Empty)}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Newest client message:");
        sb.AppendLine(transcript);

        return sb.ToString();
    }

    private static ExtractionResult Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new ExtractionResult
        {
            Category = GetString(root, "category") is { Length: > 0 } c && LegalCategories.IsValid(c)
                ? c
                : LegalCategories.Unknown,
            CategoryConfidence = GetDouble(root, "categoryConfidence"),
            NeedsMoreInfo = root.TryGetProperty("needsMoreInfo", out var n) && n.ValueKind == JsonValueKind.True,
            NextQuestion = GetString(root, "nextQuestion"),
            NarrativeSummary = GetString(root, "narrativeSummary")
        };

        if (root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in fields.EnumerateObject())
            {
                var value = GetString(prop.Value, "value");
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue; // don't persist empty fields
                }

                var isInferred = prop.Value.TryGetProperty("isInferred", out var inf) && inf.ValueKind == JsonValueKind.True;
                var note = GetString(prop.Value, "inferenceNote");

                result.Fields[prop.Name] = new ExtractedField
                {
                    Value = value,
                    IsInferred = isInferred,
                    InferenceNote = string.IsNullOrWhiteSpace(note) ? null : note
                };
            }
        }

        // Safety net: if the model claims completeness but asked no question and we still have UNKNOWN,
        // treat it as needing more info rather than a false-confident classification (Scenario N8).
        if (!result.NeedsMoreInfo
            && result.Category == LegalCategories.Unknown
            && string.IsNullOrWhiteSpace(result.NextQuestion))
        {
            result.NeedsMoreInfo = true;
            result.NextQuestion = "আপনার সমস্যাটি আমি আরেকটু ভালোভাবে বুঝতে চাই। ঘটনাটি অনুগ্রহ করে একটু বিস্তারিত বলবেন?";
        }

        return result;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;

    private static double GetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v))
        {
            return 0;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), out var d) => d,
            _ => 0
        };
    }
}
