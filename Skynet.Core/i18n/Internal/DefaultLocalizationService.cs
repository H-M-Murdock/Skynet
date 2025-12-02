using System.Globalization;

namespace Skynet.Core.i18n;

public sealed class DefaultLocalizationService : ILocalizationService
{
    private readonly ITemplateLookupService _lookupService;
    private readonly ITemplateRenderer _renderer;

    public DefaultLocalizationService(ITemplateLookupService lookupService, ITemplateRenderer renderer)
    {
        _lookupService = lookupService;
        _renderer = renderer;
    }

    public async Task<string> FormatAsync(ITenantContext tenantContext, I18NKey key, IReadOnlyDictionary<string, object?>? parameters = null, CultureInfo? requestedCulture = null, CancellationToken cancellationToken = default)
    {
        var result = await _lookupService.FindAsync(tenantContext, key, requestedCulture, cancellationToken);

        if (result.IsFound && result.Template != null)
        {
            return _renderer.Render(
                result.Template.Template, 
                parameters ?? new Dictionary<string, object?>(), 
                result.ResolvedCulture);
        }

        // Fallback: Wenn nichts gefunden, Key zurückgeben.
        return $"[{key}]";
    }
}
