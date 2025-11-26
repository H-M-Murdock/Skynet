using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Core.Tenant;

/// <summary>
/// Strategie zur Ermittlung des aktuellen Mandanten aus einem Umgebungskontext.
/// Implementierungen werten z. B. HTTP-Header, Hostnamen, JWT-Claims oder Message-Properties aus.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Versucht, den aktuellen Tenant (und ggf. seine Fallback-Kette) aus dem gegebenen Kontext aufzulösen.
    /// </summary>
    /// <param name="ambientContext">
    /// Der Kontext der aktuellen Operation. 
    /// Typischerweise <c>HttpContext</c> (bei Web), <c>MessageContext</c> (bei Messaging) oder ein Argumenten-Array (bei CLI).
    /// Implementierungen sollten prüfen, ob sie den Typ verarbeiten können.
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>
    /// Den ermittelten <see cref="ITenantContext"/>, oder <c>null</c>, 
    /// wenn dieser Resolver den Mandanten nicht bestimmen konnte (ermöglicht Chain-of-Responsibility).
    /// </returns>
    Task<ITenantContext?> ResolveAsync(object ambientContext, CancellationToken ct = default);
}