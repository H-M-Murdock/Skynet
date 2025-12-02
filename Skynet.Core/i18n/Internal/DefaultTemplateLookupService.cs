using System.Globalization;

namespace Skynet.Core.i18n;

public sealed class DefaultTemplateLookupService : ITemplateLookupService
{
    private readonly ITemplateRepository _repository;
    private readonly ITenantScopeFormatter _scopeFormatter;

    public DefaultTemplateLookupService(ITemplateRepository repository, ITenantScopeFormatter scopeFormatter)
    {
        _repository = repository;
        _scopeFormatter = scopeFormatter;
    }

    public async Task<TemplateLookupResult> FindAsync(ITenantContext tenantContext, I18NKey key, CultureInfo? requestedCulture = null, CancellationToken cancellationToken = default)
    {
        var startCulture = requestedCulture ?? tenantContext.DefaultCulture ?? CultureInfo.CurrentUICulture;
        var resolutionPath = new List<string>();

        // Wir iterieren zuerst über die Kulturen (Sprache ist meist wichtiger als Tenant-Ebene), 
        // dann über die Tenant-Chain.
        // Beispiel: User will "en-US". Wir suchen "en-US" im CurrentTenant, dann im ParentTenant.
        // Erst wenn keiner "en-US" hat, suchen wir "en" im CurrentTenant, etc.
        
        var cultures = GetCultureChain(startCulture, tenantContext.DefaultCulture);
        var tenants = tenantContext.ResolutionChain ?? Enumerable.Empty<TenantId>();

        foreach (var culture in cultures)
        {
            foreach (var tenantId in tenants)
            {
                var scope = _scopeFormatter.ToScope(tenantId);
                
                // Debug-Info
                resolutionPath.Add($"{scope}:{culture.Name}");

                var template = await _repository.TryGetExactAsync(scope, key, culture, cancellationToken);
                if (template != null)
                {
                    return TemplateLookupResult.Found(template, culture);
                }
            }
        }

        return TemplateLookupResult.NotFound(key, startCulture) with 
        { 
            ResolutionPath = string.Join(" -> ", resolutionPath) 
        };
    }

    private static IEnumerable<CultureInfo> GetCultureChain(CultureInfo start, CultureInfo? tenantDefault)
    {
        var visited = new HashSet<string>();
        
        // 1. Requested Chain (z.B. de-CH -> de -> Invariant)
        var current = start;
        while (true)
        {
            if (visited.Add(current.Name)) yield return current;
            if (current == CultureInfo.InvariantCulture) break;
            current = current.Parent;
        }

        // 2. Tenant Default Chain (falls abweichend)
        if (tenantDefault != null)
        {
            current = tenantDefault;
            while (true)
            {
                if (visited.Add(current.Name)) yield return current;
                if (current == CultureInfo.InvariantCulture) break;
                current = current.Parent;
            }
        }
    }
}
