namespace Skynet.Core.License;

public sealed record LicenseInfo(
    string Raw,
    string? LicenseId = null,
    string? Holder = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null,
    IReadOnlyDictionary<string, string>? Claims = null,
    bool? SignatureValid = null
);