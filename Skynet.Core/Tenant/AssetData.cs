namespace Skynet.Core.Tenant;

/// <summary>
/// Container für binäre Ressourcen-Daten (Assets), die über <see cref="ITenantResources"/> geladen wurden.
/// Kapselt den offenen Datenstrom und relevante Metadaten (z. B. für HTTP-Responses).
/// </summary>
/// <param name="Stream">Der offene Datenstrom. Das Asset-Objekt übernimmt die "Ownership" und schließt ihn beim Disposen.</param>
/// <param name="ContentType">Der MIME-Type (z. B. "image/png"), falls bekannt oder ermittelbar.</param>
/// <param name="FileName">Der ursprüngliche Dateiname, nützlich für "Content-Disposition" Header.</param>
public sealed record AssetData(Stream Stream, string? ContentType = null, string? FileName = null) : IDisposable
{
    /// <summary>
    /// Schließt den enthaltenen <see cref="Stream"/>.
    /// </summary>
    public void Dispose()
    {
        Stream.Dispose();
    }
}