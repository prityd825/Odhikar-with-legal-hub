using Odhikar.Application;
using Odhikar.Application.Abstractions;
using Odhikar.Infrastructure;
using Odhikar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Cloud platform PORT binding (Render, Railway, Koyeb, etc.)
var cloudPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(cloudPort))
{
    builder.WebHost.UseUrls($"http://+:{cloudPort}");
}

// Allow secrets to arrive from environment variables (e.g. Ai__GroqApiKey) as well as appsettings.
builder.Configuration.AddEnvironmentVariables();

// --- Layer Service Registrations (DDD / Clean Architecture) -------------------
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// --- Web & Presentation -------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Odhikar API", Version = "v1" });
});

// Robust CORS supporting Angular (4200), React/Next.js (3000), Vite (5173), Swagger (5080/7080), and any localhost/127.0.0.1 port
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var knownOrigins = new HashSet<string>(configuredOrigins, StringComparer.OrdinalIgnoreCase)
{
    "https://frontend-export.vercel.app",
    "http://localhost:4200",
    "https://localhost:4200",
    "http://localhost:3000",
    "https://localhost:3000",
    "http://localhost:5173",
    "https://localhost:5173",
    "http://localhost:5080",
    "https://localhost:7080"
};

builder.Services.AddCors(options =>
{
    void ConfigurePolicy(Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder policy)
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                if (knownOrigins.Contains(origin)) return true;
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                        uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                        uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }

    options.AddDefaultPolicy(ConfigurePolicy);
    options.AddPolicy("AllowAll", ConfigurePolicy);
});

var app = builder.Build();

app.UseCors();

// --- Seed on startup ----------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    var answerService = scope.ServiceProvider.GetRequiredService<IConversationAnswerService>();
    var summaryGenerator = scope.ServiceProvider.GetRequiredService<ICaseSummaryGeneratorService>();
    var appTemplateService = scope.ServiceProvider.GetRequiredService<IApplicationTemplateService>();
    try
    {
        await seeder.SeedAsync();
        await answerService.EnsureIndexesAsync();
        await summaryGenerator.EnsureIndexesAsync();
        await appTemplateService.EnsureIndexesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database seeding skipped (is MongoDB reachable?).");
    }
}

// Enable Swagger in development and production for easy evaluation and testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Odhikar.Api v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { service = "Odhikar API", status = "ok", framework = ".NET 9" }));

app.Run();
