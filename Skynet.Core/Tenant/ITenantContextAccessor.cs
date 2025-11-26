namespace Skynet.Core.Tenant;

/// <summary>
/// Ermöglicht den Zugriff auf den aktuellen <see cref="ITenantContext"/> innerhalb des logischen Ausführungspfads.
/// Implementierungen nutzen typischerweise <see cref="System.Threading.AsyncLocal{T}"/>, um die Daten über <c>await</c>-Grenzen hinweg verfügbar zu machen.
/// </summary>
public interface ITenantContextAccessor
{
    /// <summary>
    /// Ruft den aktuellen Tenant-Kontext ab oder setzt ihn manuell.
    /// Kann <c>null</c> sein, wenn der Code außerhalb eines Tenant-Scopes läuft (z. B. Hintergrund-Worker ohne Kontext).
    /// </summary>
    ITenantContext? Current { get; set; }

    /// <summary>
    /// Öffnet einen neuen Scope mit dem angegebenen Kontext.
    /// Beim Disposen des Rückgabewerts wird der vorherige Zustand wiederhergestellt (Stack-Prinzip).
    /// </summary>
    /// <param name="context">Der neue aktive Kontext.</param>
    /// <returns>Ein Disposable, das beim Aufruf den vorherigen Kontext wiederherstellt.</returns>
    IDisposable BeginScope(ITenantContext context);
}