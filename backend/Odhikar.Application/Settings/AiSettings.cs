namespace Odhikar.Application.Settings;

/// <summary>
/// Central configuration for every external AI dependency. Keys are read from
/// configuration/environment so no secret is ever hard-coded.
/// </summary>
public sealed class AiSettings
{
    public const string SectionName = "Ai";

    // Groq (ASR + optional LLM extraction)
    public string GroqApiKey { get; set; } = string.Empty;
    public string GroqBaseUrl { get; set; } = "https://api.groq.com/openai/v1";
    public string WhisperModel { get; set; } = "whisper-large-v3";
    public string GroqLlmModel { get; set; } = "llama-3.1-8b-instant";

    // Gemini (optional LLM extraction)
    public string GeminiApiKey { get; set; } = string.Empty;
    public string GeminiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string GeminiModel { get; set; } = "gemini-1.5-flash";

    /// <summary>"Groq" or "Gemini" — selects which <c>ILlmProvider</c> is used for extraction.</summary>
    public string ExtractionProvider { get; set; } = "Groq";

    // OpenRouter
    public string OpenRouterApiKey { get; set; } = string.Empty;
    public string OpenRouterBaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string OpenRouterModel { get; set; } = "google/gemini-2.0-flash-001";

    // Google Cloud Text-To-Speech (Bangla)
    public string GoogleTtsApiKey { get; set; } = string.Empty;
    public string GoogleTtsBaseUrl { get; set; } = "https://texttospeech.googleapis.com/v1";
    public string TtsLanguageCode { get; set; } = "bn-IN";
    public string TtsVoiceName { get; set; } = "bn-IN-Standard-A";
}
