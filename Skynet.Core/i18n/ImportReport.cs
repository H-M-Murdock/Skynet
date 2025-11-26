using System.Collections.Generic;

namespace Skynet.Core.i18n;

/// <summary>
/// Bericht über einen Importlauf (Erfolg, Fehler, Warnungen).
/// </summary>
public sealed record ImportReport // class -> record
{
    public bool Succeeded { get; init; }

    /// <summary>Anzahl verarbeiteter Einträge (Zeilen/Nodes je nach Format).</summary>
    public int ProcessedEntries { get; init; }

    /// <summary>Anzahl tatsächlich geschriebener Templates (bei DryRun = 0).</summary>
    public int WrittenEntries { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = new List<string>();
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();
}
