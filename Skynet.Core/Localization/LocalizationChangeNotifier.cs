// C#
using Microsoft.Extensions.Primitives;
using Skynet.Core.Tenant;

namespace Skynet.Core.Localization;

public interface ILocalizationChangeNotifier
{
    // Ein Token, das feuert, wenn die Kultur dieses Tenants geändert wurde.
    IChangeToken WatchTenantCulture(TenantId tenantId);

    // Globales Token (feuert bei irgendeiner Kulturänderung).
    IChangeToken WatchAnyCulture();

    // Wird von Store/Dekorator nach erfolgreicher Änderung aufgerufen.
    void NotifyTenantCultureChanged(TenantId tenantId);
}
