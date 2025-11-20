using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Ergebnis einer Listen-Abfrage (Keys).
/// Unterstützt Paging via ContinuationToken.
/// </summary>
public interface IResourceListResult
{
    /// <summary>Der ursprüngliche Request, der zu diesem Ergebnis führte.</summary>
    ResourceRequest Request { get; }

    /// <summary>Die gefundenen Schlüssel auf der aktuellen Seite.</summary>
    IReadOnlyList<string> Keys { get; }

    /// <summary>
    /// Token für die nächste Seite, oder null, wenn das Ende erreicht ist.
    /// </summary>
    string? ContinuationToken { get; }

    /// <summary>
    /// ID des Providers, der diese Seite geliefert hat (null bei aggregierten Ergebnissen).
    /// </summary>
    ProviderId? ProviderId { get; }
}