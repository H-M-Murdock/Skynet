namespace Skynet.Core.Tenant;

using System.Collections.Generic;
using System.Globalization;

public interface ITenantContext
{
    /// <summary>Current active tenant in this context.</summary>
    TenantId CurrentTenantId { get; }

    /// <summary>
    /// Ordered a fallback chain of tenants.
    /// The first entry should be <see cref="CurrentTenantId"/>, followed by parent or global tenants.
    /// </summary>
    IReadOnlyList<TenantId> ResolutionChain { get; }

    /// <summary>Optional human-readable name of the tenant (e.g. "ACME-EU").</summary>
    string? Name { get; }

    /// <summary>Optional description (for admin UIs, logs, docs).</summary>
    string? Description { get; }

    /// <summary>The default culture of this tenant.</summary>
    CultureInfo DefaultCulture { get; }

    /// <summary>Parent tenant in the hierarchy, if any.</summary>
    TenantId? ParentTenantId { get; }
}