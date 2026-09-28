using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Bangla speech-to-text via Groq's Whisper Large v3 audio-transcriptions endpoint.
/// Accepts WAV/WEBM (and other Whisper-supported) audio uploaded from the browser.
/// </summary>
public sealed class GroqWhisperService(HttpClient http, IOptions<AiSettings> settings, ILogger<GroqWhisperService> logger) : ISpeechToTextService
{
    private readonly HttpClient _http = http;
    private readonly AiSettings _settings = settings.Value;
    private readonly ILogger<GroqWhisperService> _logger = logger;

    public async Task<string> TranscribeAsync(Stream audioStream, string fileName, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GroqApiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured (Ai:GroqApiKey).");
        }

        using var form = new MultipartFormDataContent();

        // Strip parameter attributes (e.g. ';codecs=opus') to prevent MediaTypeHeaderValue FormatException
        var mediaType = "audio/webm";
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var clean = contentType.Split(';')[0].Trim();
            if (!string.IsNullOrWhiteSpace(clean))
            {
                mediaType = clean;
            }
        }

        var audioContent = new StreamContent(audioStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        var effectiveFileName = string.IsNullOrWhiteSpace(fileName) ? "record.webm" : fileName;
        if (!Path.HasExtension(effectiveFileName))
        {
            effectiveFileName += ".webm";
        }
        form.Add(audioContent, "file", effectiveFileName);

        form.Add(new StringContent(_settings.WhisperModel), "model");
        form.Add(new StringContent("bn"), "language"); // Bangla
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent("0"), "temperature");
        form.Add(new StringContent("বাংলা কথোপকথন, অধিকার ও আইনি সহায়তা।"), "prompt");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.GroqBaseUrl}/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.GroqApiKey);
        request.Content = form;

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Groq Whisper call failed ({Status}): {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(body);
        var transcript = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString()?.Trim() ?? string.Empty : string.Empty;

        if (IsSilenceHallucination(transcript))
        {
            _logger.LogInformation("Whisper silence hallucination filtered out: '{Transcript}'", transcript);
            return string.Empty;
        }

        return transcript;
    }

    private static bool IsSilenceHallucination(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var words = text.Split(new[] { ' ', '।', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;

        // Filter repeating silence tokens commonly hallucinated by Whisper in Bengali
        if (words.All(w => w.Equals("প্রাক্ষন", StringComparison.OrdinalIgnoreCase) ||
                           w.Equals("প্রাক্তন", StringComparison.OrdinalIgnoreCase) ||
                           w.Equals("ধন্যবাদ", StringComparison.OrdinalIgnoreCase) ||
                           w.Equals("you", StringComparison.OrdinalIgnoreCase) ||
                           w.Equals("subtitles", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Repetitive loops on low audio / silence
        if (words.Length >= 3 && words.Distinct().Count() == 1)
        {
            return true;
        }

        return false;
    }
}
