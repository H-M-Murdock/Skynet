// Skynet.Core.Tenant/ITenantResolver.cs
using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Core.Tenant;

/// <summary>
/// Strategien zur Auflösung des aktuellen Tenants aus einer Umgebung (HTTP, Messaging, CLI).
/// Implementierungen können z. B. Header, Token, Route, Message-Envelope auswerten.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Löst den aktuellen Tenant (und ggf. seine Fallback-Kette) aus dem gegebenen Kontext auf.
    /// Muss deterministisch und leichtgewichtig sein; I/O sollte vermieden oder gecacht werden.
    /// </summary>
    Task<ITenantContext> ResolveAsync(object ambientContext, CancellationToken ct = default);
}
