using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Core.i18n;

/// <summary>
/// Strukturierte, lokalisierte Exception-Repräsentation.
/// </summary>
public sealed class LocalizedException
{
    public I18NKey Key { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string? Code { get; init; }
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }
}
