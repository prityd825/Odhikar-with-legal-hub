using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.Services;

/// <summary>
/// Bangla Text-To-Speech via Google Cloud TTS REST API. Returns MP3 audio bytes.
/// If no API key is configured it returns an empty array so callers can degrade
/// gracefully (the UI simply hides the audio player).
/// </summary>
public sealed class GoogleTtsService(HttpClient http, IOptions<AiSettings> settings, ILogger<GoogleTtsService> logger) : ITextToSpeechService
{
    private readonly HttpClient _http = http;
    private readonly AiSettings _settings = settings.Value;
    private readonly ILogger<GoogleTtsService> _logger = logger;

    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // 1. If Google Cloud TTS API Key is configured, attempt official Google Cloud TTS REST API
        if (!string.IsNullOrWhiteSpace(_settings.GoogleTtsApiKey))
        {
            try
            {
                var url = $"{_settings.GoogleTtsBaseUrl}/text:synthesize?key={_settings.GoogleTtsApiKey}";

                var payload = new
                {
                    input = new { text },
                    voice = new { languageCode = _settings.TtsLanguageCode, name = _settings.TtsVoiceName },
                    audioConfig = new { audioEncoding = "MP3", speakingRate = 0.95 }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                using var response = await _http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("audioContent", out var audio))
                    {
                        var base64 = audio.GetString();
                        if (!string.IsNullOrEmpty(base64))
                        {
                            return Convert.FromBase64String(base64);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Google Cloud TTS call returned {Status}. Falling back to public Bangla TTS engine.", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Official Google Cloud TTS call failed. Falling back to public Bangla TTS engine.");
            }
        }

        // 2. High-quality Bangla TTS engine fallback (handles long paragraphs via smart chunking)
        try
        {
            var chunks = SplitIntoTtsChunks(text, 140);
            if (chunks.Count == 0) return [];

            var audioStreams = new List<byte[]>();
            foreach (var chunk in chunks)
            {
                var fallbackUrl = $"https://translate.google.com/translate_tts?ie=UTF-8&tl=bn&client=tw-ob&q={Uri.EscapeDataString(chunk)}";
                using var fallbackReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
                fallbackReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                using var fallbackRes = await _http.SendAsync(fallbackReq, ct);
                if (fallbackRes.IsSuccessStatusCode)
                {
                    var audioBytes = await fallbackRes.Content.ReadAsByteArrayAsync(ct);
                    if (audioBytes.Length > 0)
                    {
                        audioStreams.Add(audioBytes);
                    }
                }
                else
                {
                    _logger.LogWarning("Bangla TTS fallback returned {Status} for chunk: {Chunk}", fallbackRes.StatusCode, chunk);
                }
            }

            if (audioStreams.Count == 0) return [];
            if (audioStreams.Count == 1) return audioStreams[0];

            // Concatenate all MP3 chunks together
            using var combined = new MemoryStream();
            foreach (var chunkAudio in audioStreams)
            {
                await combined.WriteAsync(chunkAudio, ct);
            }
            return combined.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bangla TTS synthesis fallback failed.");
        }

        return [];
    }

    private static List<string> SplitIntoTtsChunks(string text, int maxChunkSize)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        // Split by sentences or punctuations first
        var sentences = text.Split(new[] { '।', '.', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();

        foreach (var rawSentence in sentences)
        {
            var sentence = rawSentence.Trim();
            if (string.IsNullOrWhiteSpace(sentence)) continue;

            if (sentence.Length > maxChunkSize)
            {
                // Split long sentence by spaces / words
                var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    if (currentChunk.Length + word.Length + 1 > maxChunkSize)
                    {
                        if (currentChunk.Length > 0)
                        {
                            result.Add(currentChunk.ToString().Trim());
                            currentChunk.Clear();
                        }
                    }
                    if (currentChunk.Length > 0) currentChunk.Append(' ');
                    currentChunk.Append(word);
                }
            }
            else
            {
                if (currentChunk.Length + sentence.Length + 1 > maxChunkSize)
                {
                    result.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }
                if (currentChunk.Length > 0) currentChunk.Append("। ");
                currentChunk.Append(sentence);
            }
        }

        if (currentChunk.Length > 0)
        {
            result.Add(currentChunk.ToString().Trim());
        }

        return result;
    }
}
