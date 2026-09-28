using System.Text.Json;
using Odhikar.Application.Abstractions;

namespace Odhikar.Application.Services;

/// <summary>
/// Two-layer safety net. A deterministic Bangla keyword matcher runs first (always
/// available, zero-latency), then an optional LLM classifier catches paraphrased
/// distress the keyword list misses. Either layer firing escalates the case.
/// </summary>
public sealed class SafetyGuardrailService(ILlmProviderResolver resolver, ILogger<SafetyGuardrailService> logger) : ISafetyGuardrailService
{
    private readonly ILlmProviderResolver _resolver = resolver;
    private readonly ILogger<SafetyGuardrailService> _logger = logger;

    // Deterministic fallback signals — physical harm, threats, fear, violence.
    private static readonly string[] DangerKeywords =
    [
        "হাত ভেঙে", "মেরে ফেলব", "মেরে ফেলবে", "রক্ত", "ভয় পাচ্ছি", "ভয়ে আছি",
        "মারধর", "মারবে", "গলা টিপে", "আত্মহত্যা", "জানে মেরে", "খুন করবে", "খুন",
        "হত্যা", "ছুরি", "আগুন দিয়ে", "অ্যাসিড", "বিষ খাইয়ে", "বিষ", "অস্ত্র",
        "দাঙ্গা", "আক্রমণ", "মারতে আসছে", "বাঁচাও", "বাঁচান", "বিপদে আছি", "বিপদ"
    ];

    public async Task<SafetyResult> ScanAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SafetyResult.Safe();
        }

        // Layer 1 — deterministic keyword match.
        var keyword = DangerKeywords.FirstOrDefault(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (keyword is not null)
        {
            return new SafetyResult(
                true,
                $"ক্লায়েন্টের বক্তব্যে সম্ভাব্য সহিংসতা/হুমকির ইঙ্গিত পাওয়া গেছে (\"{keyword}\").",
                keyword);
        }

        // Layer 2 — LLM classifier (best-effort; never blocks intake on failure).
        try
        {
            var result = await ClassifyWithLlmAsync(text, ct);
            if (result is not null && result.IsTriggered)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Safety LLM classification failed; relying on keyword layer only.");
        }

        return SafetyResult.Safe();
    }

    private async Task<SafetyResult?> ClassifyWithLlmAsync(string text, CancellationToken ct)
    {
        const string system =
            "You are a critical safety classifier for a Bangladeshi women-rights legal-aid intake platform. " +
            "Your ONLY job is to detect IMMEDIATE, ACTIVE, LIFE-THREATENING EMERGENCIES happening RIGHT NOW that require instant police/paramedic response (e.g. active ongoing attack, knife assault in progress, immediate suicide attempt, or urgent hiding from active pursuer). " +
            "CRITICAL RULE: DO NOT flag past abuse, historical domestic violence, past physical assaults, past dowry demands, or past eviction where the victim is now recounting what happened or is in a safe location (e.g. 'আমার স্বামী আমাকে নির্যাতন করেছে', 'মারধর করে বের করে দিয়েছিল', 'বাবার বাড়ি আছি'). Those are normal legal complaints for legal aid and MUST return danger: false. " +
            "Respond ONLY with valid JSON: {\"danger\": true|false, \"reason\": \"<short Bangla reason if true, else null>\"}.";

        var raw = await _resolver.Resolve().CompleteJsonAsync(system, text, ct);

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var danger = root.TryGetProperty("danger", out var d) && d.ValueKind == JsonValueKind.True;
        if (!danger)
        {
            return SafetyResult.Safe();
        }

        var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
        return new SafetyResult(true, reason ?? "সম্ভাব্য নিরাপত্তা ঝুঁকি শনাক্ত হয়েছে।", "llm-classifier");
    }
}
