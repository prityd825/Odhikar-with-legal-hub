using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odhikar.Application.Dtos;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Service that interacts directly with Google Gemini 1.5 Flash REST API
/// for multimodal audio processing (STT) and multi-turn legal intake dialogue in Bangla.
/// </summary>
public sealed class GeminiChatService(
    HttpClient http,
    IOptions<AiSettings> settings,
    ILogger<GeminiChatService> logger)
{
    private readonly HttpClient _http = http;
    private readonly AiSettings _settings = settings.Value;
    private readonly ILogger<GeminiChatService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SystemInstruction = """
        You are a warm, empathetic, and professional Legal Aid Intake Assistant in Bangladesh (Project Odhikar).
        Your mission is to interview citizens seeking legal aid, assess urgency and safety risks, and extract structured legal facts.

        STRICT RULES:
        1. Safety Escalation:
           If the citizen discloses active physical violence, domestic abuse, death threats, or life-threatening emergencies, IMMEDIATELY set:
           - "isSafetyEscalated": true
           - "safetyReason": Concise explanation in Bangla of the immediate risk and advice to call 16430 (National Legal Aid) or 999.
           - "isComplete": true
           - "nextFollowUpQuestion": null

        2. Follow-up Loop & Conversational Questions:
           - Check what key facts are still missing:
             * Applicant name/identity
             * Respondent name/identity
             * Date/time of marriage or incident
             * Disputed amount (e.g., dower/wages in BDT, or land size)
             * Available proofs/evidence (e.g., Nikahnama, salary slip, land deed, medical certificate)
           - If information is missing and total turns < 4:
             * Formulate exactly ONE short, supportive, conversational question in natural colloquial Bangla.
             * Set "nextFollowUpQuestion" to this single question.
             * Set "isComplete": false.

        3. Completion:
           - If the core facts are already sufficient, OR if the conversation has completed 4 turns:
             * Set "isComplete": true.
             * Set "nextFollowUpQuestion": null.

        4. Output Schema:
           You must strictly return a valid JSON object matching this schema:
           {
             "isSafetyEscalated": boolean,
             "safetyReason": string or null,
             "isComplete": boolean,
             "category": string (e.g., "দেনমোহর ও খোরপোষ", "যৌতুক ও পারিবারিক নির্যাতন", "বকেয়া বেতন", "জমি বিরোধ", or "সাধারণ আইনি সমস্যা"),
             "nextFollowUpQuestion": string or null,
             "extractedData": {
               "applicant": { "value": string or null, "isInferred": boolean },
               "respondent": { "value": string or null, "isInferred": boolean },
               "marriageOrIncidentDate": { "value": string or null, "isInferred": boolean },
               "disputedAmount": { "value": string or null, "isInferred": boolean },
               "evidenceList": [ string ]
             }
           }
        """;

    /// <summary>
    /// Multimodal Audio Transcription using Gemini 1.5 Flash.
    /// Accepts raw audio bytes (audio/webm, audio/wav, audio/ogg, audio/mp4) and transcribes Bangla speech.
    /// </summary>
    public async Task<string> TranscribeAudioAsync(Stream audioStream, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
        {
            throw new InvalidOperationException("Google Gemini API Key is not configured (Ai:GeminiApiKey).");
        }

        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, ct);
        var audioBytes = memoryStream.ToArray();

        if (audioBytes.Length == 0) return string.Empty;

        var cleanMediaType = "audio/webm";
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            cleanMediaType = contentType.Split(';')[0].Trim();
        }

        var base64Audio = Convert.ToBase64String(audioBytes);

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Please transcribe the speech in this audio recording accurately in natural Bengali (বাংলা) script. Output ONLY the transcribed Bengali text, with no preamble, formatting, or extra commentary." },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = cleanMediaType,
                                data = base64Audio
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1
            }
        };

        var candidateModels = new[] { _settings.GeminiModel, "gemini-3.6-flash", "gemini-3.5-flash", "gemini-flash-latest" }
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToArray();

        HttpResponseMessage? response = null;
        string? responseBody = null;

        try
        {
            foreach (var model in candidateModels)
            {
                var url = $"{_settings.GeminiBaseUrl}/models/{model}:generateContent?key={_settings.GeminiApiKey}";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                response?.Dispose();
                response = await _http.SendAsync(httpRequest, ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                _logger.LogInformation("Gemini model '{Model}' returned {StatusCode}. Trying next fallback...", model, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini audio transcription failed.");
            return string.Empty;
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini audio transcription returned {Status}: {Body}", response?.StatusCode, responseBody);
            return string.Empty;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(responseBody)) return string.Empty;
            using var doc = JsonDocument.Parse(responseBody);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return string.Empty;

            var text = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini transcription JSON.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Processes a single multi-turn dialogue turn using Google Gemini 1.5 Flash with structured JSON output.
    /// </summary>
    public async Task<ExtractionResponseDto> ProcessTurnAsync(ConversationRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
        {
            _logger.LogWarning("Gemini API key is not configured.");
            return GenerateResilientFallback(request, "Google Gemini API Key কনফিগার করা নেই।");
        }

        var contents = new List<object>();

        // Map prior history turns
        if (request.History != null && request.History.Count > 0)
        {
            foreach (var turn in request.History)
            {
                if (string.IsNullOrWhiteSpace(turn.Text)) continue;
                var role = string.Equals(turn.Role, "model", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "model"
                    : "user";

                contents.Add(new
                {
                    role,
                    parts = new[] { new { text = turn.Text } }
                });
            }
        }

        // Contextualize current user input with selected Incident Type
        var contextualInput = contents.Count == 0
            ? $"[নির্বাচিত আইনি বিষয়: {request.IncidentType}]\n{request.CurrentInput}"
            : request.CurrentInput;

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = contextualInput } }
        });

        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = SystemInstruction } }
            },
            contents,
            generationConfig = new
            {
                temperature = 0.2,
                response_mime_type = "application/json"
            }
        };

        var candidateModels = new[] { _settings.GeminiModel, "gemini-3.6-flash", "gemini-3.5-flash", "gemini-flash-latest" }
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToArray();

        HttpResponseMessage? response = null;
        string? responseBody = null;

        try
        {
            foreach (var model in candidateModels)
            {
                var url = $"{_settings.GeminiBaseUrl}/models/{model}:generateContent?key={_settings.GeminiApiKey}";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                response?.Dispose();
                response = await _http.SendAsync(httpRequest, ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                _logger.LogInformation("Gemini model '{Model}' returned {StatusCode}. Trying next candidate...", model, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini HTTP request encountered an error or timeout. Providing safe fallback response.");
            return GenerateResilientFallback(request, "Gemini সার্ভিসের সংযোগে বিলম্ব হয়েছে।");
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini generateContent call failed ({Status}): {Body}", response?.StatusCode, responseBody);
            return GenerateResilientFallback(request, "Gemini সার্ভিসের সাথে সংযোগে সমস্যা হয়েছে।");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return GenerateResilientFallback(request, "খালি উত্তর পাওয়া গেছে।");
            }
            using var doc = JsonDocument.Parse(responseBody);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
            {
                return GenerateResilientFallback(request, "কোনো উত্তর পাওয়া যায়নি।");
            }

            var textPart = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(textPart))
            {
                return GenerateResilientFallback(request, "খালি উত্তর পাওয়া গেছে।");
            }

            var parsed = JsonSerializer.Deserialize<ExtractionResponseDto>(textPart, JsonOptions);
            return parsed ?? GenerateResilientFallback(request, "তথ্য পার্স করা সম্ভব হয়নি।");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini JSON response: {Raw}", responseBody);
            return GenerateResilientFallback(request, "তথ্য প্রক্রিয়াকরণে ত্রুটি হয়েছে।");
        }
    }

    private static ExtractionResponseDto GenerateResilientFallback(ConversationRequestDto req, string message)
    {
        // Immediate Safety Keyword Screening
        var isUrgent = ContainsSafetyTrigger(req.CurrentInput);
        if (isUrgent)
        {
            return new ExtractionResponseDto(
                IsSafetyEscalated: true,
                SafetyReason: "শারীরিক নির্যাতন, মারাত্মক হুমকি বা জীবনের ঝুঁকির আশঙ্কা সনাক্ত হয়েছে। আপনার সুরক্ষায় অবিলম্বে জাতীয় আইনগত সহায়তা হেল্পলাইন ১৬৪৩০ বা জরুরি সেবা ৯৯৯ এ যোগাযোগ করার পরামর্শ দেওয়া হচ্ছে।",
                IsComplete: true,
                Category: req.IncidentType ?? "যৌতুক ও পারিবারিক নির্যাতন",
                NextFollowUpQuestion: null,
                ExtractedData: new ExtractedLegalData(
                    Applicant: null,
                    Respondent: null,
                    MarriageOrIncidentDate: null,
                    DisputedAmount: null,
                    EvidenceList: []
                )
            );
        }

        return new ExtractionResponseDto(
            IsSafetyEscalated: false,
            SafetyReason: null,
            IsComplete: false,
            Category: req.IncidentType ?? "সাধারণ আইনি সমস্যা",
            NextFollowUpQuestion: $"আপনার বক্তব্যটি পেয়েছি। {message} ঘটনার স্থান, তারিখ অথবা প্রমাণপত্র সম্পর্কে আরও কিছু বলুন?",
            ExtractedData: new ExtractedLegalData(
                Applicant: null,
                Respondent: null,
                MarriageOrIncidentDate: null,
                DisputedAmount: null,
                EvidenceList: []
            )
        );
    }

    private static bool ContainsSafetyTrigger(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        string[] triggers = ["মারধর", "খুন", "হত্যা", "অস্ত্র", "দাঙ্গা", "suicide", "kill", "weapon", "rape", "আক্রমণ", "হুমকি"];
        return triggers.Any(t => lower.Contains(t));
    }
}
