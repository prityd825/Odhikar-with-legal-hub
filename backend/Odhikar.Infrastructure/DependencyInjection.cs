using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;
using Odhikar.Infrastructure.ExternalServices.Ai;
using Odhikar.Infrastructure.Persistence;
using Odhikar.Infrastructure.Services;
using Polly;
using Polly.Extensions.Http;

namespace Odhikar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Options Configuration ----------------------------------------------------
        services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));
        services.Configure<AiSettings>(configuration.GetSection(AiSettings.SectionName));
        services.Configure<QuestionCategorySettings>(configuration.GetSection(QuestionCategorySettings.SectionName));
        services.Configure<FollowUpPrioritySettings>(configuration.GetSection("FollowUpPriority"));

        // --- Persistence --------------------------------------------------------------
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<DbSeeder>();
        services.AddScoped<IConversationRepository, ConversationRepository>();

        // --- Retry Policy for External HTTP APIs --------------------------------------
        IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
            HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(4, (attempt, outcome, _) =>
                {
                    var retryAfter = outcome.Result?.Headers.RetryAfter?.Delta;
                    return retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                },
                (_, _, _, _) => Task.CompletedTask);

        // --- HTTP Clients -------------------------------------------------------------
        services.AddHttpClient<GroqWhisperService>(c => c.Timeout = TimeSpan.FromMinutes(2))
            .AddPolicyHandler(RetryPolicy());
        services.AddHttpClient<GroqLlmProvider>(c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddPolicyHandler(RetryPolicy());
        services.AddHttpClient<GeminiLlmProvider>(c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddPolicyHandler(RetryPolicy());
        services.AddHttpClient<GeminiChatService>(c => c.Timeout = TimeSpan.FromSeconds(90))
            .AddPolicyHandler(RetryPolicy());
        services.AddHttpClient<GeminiService>(c => c.Timeout = TimeSpan.FromSeconds(90))
            .AddPolicyHandler(RetryPolicy());
        services.AddHttpClient<GoogleTtsService>(c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddPolicyHandler(RetryPolicy());
        services.AddHttpClient<OpenRouterLlmService>(c => c.Timeout = TimeSpan.FromSeconds(60))
            .AddPolicyHandler(RetryPolicy());

        // --- LLM Providers & Resolver -------------------------------------------------
        services.AddSingleton<ILlmProvider>(sp => sp.GetRequiredService<GroqLlmProvider>());
        services.AddSingleton<ILlmProvider>(sp => sp.GetRequiredService<GeminiLlmProvider>());
        services.AddSingleton<LlmProviderResolver>();
        services.AddSingleton<ILlmProviderResolver>(sp => sp.GetRequiredService<LlmProviderResolver>());

        // --- External & Infrastructure Services ---------------------------------------
        services.AddScoped<IOpenRouterLlmService, OpenRouterLlmService>();
        services.AddScoped<ISpeechToTextService>(sp => sp.GetRequiredService<GroqWhisperService>());
        services.AddScoped<ITextToSpeechService>(sp => sp.GetRequiredService<GoogleTtsService>());
        services.AddScoped<IAiExtractionService, AiExtractionService>();
        services.AddSingleton<IPdfService, QuestPdfService>();

        // --- Data-Driven Domain Services ----------------------------------------------
        services.AddScoped<ICaseService, CaseService>();
        services.AddSingleton<IQuestionBankService, QuestionBankService>();
        services.AddSingleton<IQuestionCategoryService, QuestionCategoryService>();
        services.AddScoped<IParentCategoryService, ParentCategoryService>();
        services.AddScoped<ICaseSummaryTemplateService, CaseSummaryTemplateService>();
        services.AddScoped<IConversationAnswerService, ConversationAnswerService>();
        services.AddScoped<ILegalFactExtractorService, LegalFactExtractorService>();
        services.AddScoped<ICaseSummaryGeneratorService, CaseSummaryGeneratorService>();
        services.AddScoped<IApplicationTemplateService, ApplicationTemplateService>();

        // --- Conversational Orchestration ---------------------------------------------
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<ConversationOrchestrator>();
        services.AddScoped<IConversationOrchestrator>(sp => sp.GetRequiredService<ConversationOrchestrator>());
        services.AddScoped<IConversationService>(sp => sp.GetRequiredService<ConversationOrchestrator>());

        return services;
    }
}
