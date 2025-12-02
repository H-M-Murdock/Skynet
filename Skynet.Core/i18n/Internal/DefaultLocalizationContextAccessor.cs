using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Skynet.Core.i18n;

public sealed class DefaultLocalizationContextAccessor : ILocalizationContextAccessor
{
    public CultureInfo GetActiveCulture(ITenantContext tenantContext)
    {
        // Standard .NET Verhalten: CurrentUICulture nutzen
        return CultureInfo.CurrentUICulture;
    }

    public void SetActiveCulture(ITenantContext tenantContext, CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }

    public (CultureInfo culture, IChangeToken token) GetActiveCultureWithWatch(ITenantContext tenantContext)
    {
        // Einfache Implementierung ohne echtes Change-Token für den Start
        return (CultureInfo.CurrentUICulture, NullChangeToken.Singleton);
    }
}
