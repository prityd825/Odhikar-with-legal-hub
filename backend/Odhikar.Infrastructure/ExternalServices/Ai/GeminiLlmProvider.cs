using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.ExternalServices.Ai;

/// <summary>
/// LLM provider backed by Google Gemini 1.5 Flash with JSON response MIME type.
/// </summary>
public sealed class GeminiLlmProvider(HttpClient http, IOptions<AiSettings> settings, ILogger<GeminiLlmProvider> logger) : ILlmProvider
{
    private readonly HttpClient _http = http;
    private readonly AiSettings _settings = settings.Value;
    private readonly ILogger<GeminiLlmProvider> _logger = logger;

    public string Name => "Gemini";

    public async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured (Ai:GeminiApiKey).");
        }

        var url = $"{_settings.GeminiBaseUrl}/models/{_settings.GeminiModel}:generateContent?key={_settings.GeminiApiKey}";

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                response_mime_type = "application/json"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini LLM call failed ({Status}): {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        return JsonSanitizer.StripCodeFences(content);
    }
}
