using Microsoft.Extensions.Options;
using Odhikar.Application.Abstractions;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Picks the active <see cref="ILlmProvider"/> based on <c>Ai:ExtractionProvider</c>,
/// falling back to the first registered provider if the configured name is unknown.
/// </summary>
public sealed class LlmProviderResolver(IEnumerable<ILlmProvider> providers, IOptions<AiSettings> settings) : ILlmProviderResolver
{
    private readonly IReadOnlyList<ILlmProvider> _providers = [.. providers];
    private readonly AiSettings _settings = settings.Value;

    public ILlmProvider Resolve()
    {
        var configured = _settings.ExtractionProvider;
        var match = _providers.FirstOrDefault(p =>
            string.Equals(p.Name, configured, StringComparison.OrdinalIgnoreCase));

        return match
            ?? _providers.FirstOrDefault()
            ?? throw new InvalidOperationException("No ILlmProvider is registered.");
    }
}
