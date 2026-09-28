using Microsoft.Extensions.DependencyInjection;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Services;

namespace Odhikar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISafetyGuardrailService, SafetyGuardrailService>();
        services.AddScoped<IFollowUpQuestionEngine, FollowUpQuestionEngine>();
        services.AddSingleton<ITemplateEngineService, TemplateEngineService>();
        services.AddSingleton<IConversationStateValidator, ConversationStateValidator>();
        return services;
    }
}
