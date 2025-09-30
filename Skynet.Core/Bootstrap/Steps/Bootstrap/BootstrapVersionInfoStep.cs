// Skynet.Core.Bootstrap/Steps/BootstrapVersionStep.cs

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapVersionStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;
    private AppVersionInfo? _info;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = entry.GetName();

        var assemblyVersion = name.Version?.ToString() ?? "n/a";
        var fileVersion = entry.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "n/a";
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "n/a";

        _info = new AppVersionInfo
        {
            Product = name.Name ?? "Unknown",
            AssemblyVersion = assemblyVersion,
            FileVersion = fileVersion,
            InformationalVersion = informational,
            BuildDateUtc = TryGetLinkerTimestampUtc(entry)
        };

        services.AddSingleton(_info);

        _report = $"version product='{_info.Product}', asm='{_info.AssemblyVersion}', file='{_info.FileVersion}', info='{_info.InformationalVersion}', buildUtc={_info.BuildDateUtc:O}";
        return Task.CompletedTask;
    }

    public string GetReport() => _report;

    // Optional: Buildzeitpunkt heuristisch über PE-Header ermitteln (falls verfügbar); sonst DateTime.MinValue
    private static DateTime TryGetLinkerTimestampUtc(Assembly asm)
    {
        try
        {
            var path = asm.Location;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return DateTime.MinValue;

            // PE-Header lesen (klassische Heuristik)
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            fs.Seek(0x3C, SeekOrigin.Begin);
            var peHeaderOffset = br.ReadInt32();
            fs.Seek(peHeaderOffset + 8, SeekOrigin.Begin); // 8 bytes to PE timestamp
            var secondsSince1970 = br.ReadInt32();

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(secondsSince1970);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}

public sealed class AppVersionInfo
{
    public string Product { get; set; } = "Unknown";
    public string AssemblyVersion { get; set; } = "n/a";
    public string FileVersion { get; set; } = "n/a";
    public string InformationalVersion { get; set; } = "n/a";
    public DateTime BuildDateUtc { get; set; }
}
