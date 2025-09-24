using System.Globalization;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Optional hints for a resource query (e.g., culture override).
/// </summary>
public sealed record ResourceQueryOptions(
    CultureInfo? CultureOverride = null
);