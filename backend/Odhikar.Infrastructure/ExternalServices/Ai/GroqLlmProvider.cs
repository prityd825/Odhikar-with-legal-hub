using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.ExternalServices.Ai;

/// <summary>
/// LLM provider backed by Groq's OpenAI-compatible chat-completions endpoint using
/// Llama 3.1 8B Instant with JSON response formatting.
/// </summary>
public sealed class GroqLlmProvider(HttpClient http, IOptions<AiSettings> settings, ILogger<GroqLlmProvider> logger) : ILlmProvider
{
    private readonly HttpClient _http = http;
    private readonly AiSettings _settings = settings.Value;
    private readonly ILogger<GroqLlmProvider> _logger = logger;

    public string Name => "Groq";

    public async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GroqApiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured (Ai:GroqApiKey).");
        }

        var payload = new
        {
            model = _settings.GroqLlmModel,
            temperature = 0.2,
            max_tokens = 500,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.GroqBaseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.GroqApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Groq LLM call failed ({Status}): {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        return JsonSanitizer.StripCodeFences(content);
    }
}
