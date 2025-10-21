using System.Globalization;
using Skynet.Core;
using Skynet.Core.Time;

namespace Skynet.Core.Logging;

/// <summary>
/// Rotation-Policy für tägliche Log-Dateien:
/// - ResolveTarget: bildet einen absoluten Pfad basierend auf BaseRoot/Tenant/SubFolder + gerendertem Template.
/// - ShouldRotate: true, wenn der Kalendertag gewechselt hat (Taggrenze via IClock; UTC oder lokal konfigurierbar).
/// Größe wird hier nicht berücksichtigt (dafür kann FileLogSinkOptions.MaxFileBytes greifen).
/// </summary>
public sealed class DayRotationPolicy : IFileRotationPolicy
{
    private readonly IPathTemplateRenderer _renderer;
    private readonly IClock _clock;
    private readonly string _baseRootFull;
    private readonly string _tenant;
    private readonly string? _subFolder;
    private readonly string _pathTemplate;
    private readonly bool _useUtcForDayBoundary;

    /// <param name="renderer">Renderer für das Template (liefert relativen Key).</param>
    /// <param name="clock">Zeitquelle (UTC-basiert) aus dem Time-Feature.</param>
    /// <param name="baseRootFull">Absoluter Basis-Root.</param>
    /// <param name="tenant">Tenant-Ordner unterhalb des Root.</param>
    /// <param name="pathTemplate">Template z. B. "{yyyy-MM-dd}/{level}.log".</param>
    /// <param name="subFolder">Optionaler Subfolder unterhalb des Tenants.</param>
    /// <param name="useUtcForDayBoundary">true = Taggrenze anhand UTC; false = nach lokaler Zeit.</param>
    public DayRotationPolicy(
        IPathTemplateRenderer renderer,
        IClock clock,
        string baseRootFull,
        string tenant,
        string pathTemplate,
        string? subFolder = null,
        bool useUtcForDayBoundary = true)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (string.IsNullOrWhiteSpace(baseRootFull)) throw new ArgumentNullException(nameof(baseRootFull));
        if (string.IsNullOrWhiteSpace(tenant)) throw new ArgumentNullException(nameof(tenant));
        if (string.IsNullOrWhiteSpace(pathTemplate)) throw new ArgumentNullException(nameof(pathTemplate));

        _baseRootFull = Path.GetFullPath(baseRootFull);
        _tenant = tenant;
        _subFolder = subFolder;
        _pathTemplate = pathTemplate;
        _useUtcForDayBoundary = useUtcForDayBoundary;
    }

    public FileTarget ResolveTarget(ILogEvent evt, DateTimeOffset now)
    {
        // Für Konsistenz wird das Template mit "now" gerendert (Tag-/Datumslogik kommt vom Aufrufer).
        string relativeKey =
            _renderer is DefaultPathTemplateRenderer dr
                ? dr.Render(evt, now, _pathTemplate)
                : _renderer.Render(evt, now);

        var full = IoUtilities.BuildSafeFullPath(_baseRootFull, _tenant, relativeKey, _subFolder);
        return new FileTarget(full);
    }

    public bool ShouldRotate(FileTarget target, long currentBytes, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(target.FullPath)) return false;

        try
        {
            var fi = new FileInfo(target.FullPath);
            if (!fi.Exists) return false;

            // Referenzzeit über IClock (UTC)
            var utcNow = _clock.UtcNow;
            var current = _useUtcForDayBoundary ? utcNow : utcNow.ToLocalTime();
            var last = _useUtcForDayBoundary ? fi.LastWriteTimeUtc : fi.LastWriteTime;

            var lastDate = DateOnly.FromDateTime(last);
            var currDate = DateOnly.FromDateTime(current);

            return currDate > lastDate;
        }
        catch
        {
            return false;
        }
    }
}
