using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Skynet.Core.Localization;

/// <summary>
/// No-Op Implementierung für den Start.
/// </summary>
public sealed class DefaultLocalizationChangeNotifier : ILocalizationChangeNotifier
{
    public IChangeToken WatchTenantCulture(TenantId tenantId) 
        => NullChangeToken.Singleton;

    public IChangeToken WatchAnyCulture() 
        => NullChangeToken.Singleton;

    public void NotifyTenantCultureChanged(TenantId tenantId)
    {
        // In einer echten Implementierung würde hier ein CancellationTokenSource gecancelt werden.
    }
}
