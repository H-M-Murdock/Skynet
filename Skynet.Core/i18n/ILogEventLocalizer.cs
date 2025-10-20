using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Skynet.Core.Logging;
using Skynet.Core.Tenant;

namespace Skynet.Core.i18n;

/// <summary>
/// Optional: Lokalisierung von Log-Events anhand MessageTemplate + Properties.
/// Kann für UI/Reports genutzt werden, ohne die ursprünglichen Templates zu verlieren.
/// </summary>
public interface ILogEventLocalizer
{
    /// <summary>
    /// Rendert eine lokalisierte Log-Nachricht.
    /// </summary>
    Task<string> FormatAsync(
        ITenantContext tenantContext,
        ILogEvent logEvent,
        CultureInfo? requestedCulture = null,
        CancellationToken cancellationToken = default);
}
