// Skynet.Core.Bootstrap/Steps/BootstrapTempStep.cs
using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap.Steps;

public sealed class BootstrapTempStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private readonly string _appSubfolderName;
    private string _report = string.Empty;

    public BootstrapTempStep(string appSubfolderName = "Skynet")
    {
        // Unterordner im OS-Temp (z. B. C:\Users\<user>\AppData\Local\Temp\Skynet oder /tmp/Skynet)
        _appSubfolderName = string.IsNullOrWhiteSpace(appSubfolderName) ? "Skynet" : appSubfolderName;
    }

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        var baseTemp = Path.GetTempPath(); // OS-Temp
        var tempDir = Path.Combine(baseTemp, _appSubfolderName);

        var result = new TempDirectoryState { Directory = tempDir };

        try
        {
            Directory.CreateDirectory(tempDir);

            // Write
            var probeFile = Path.Combine(tempDir, ".tmp_probe");
            var payload = $"probe:{DateTime.UtcNow:O}";
            File.WriteAllText(probeFile, payload);

            // Read
            var read = File.ReadAllText(probeFile);
            result.CanWrite = true;
            result.CanRead = read == payload;

            // Delete
            File.Delete(probeFile);
            result.CanDelete = !File.Exists(probeFile);

            result.IsUsable = result.CanWrite && result.CanRead && result.CanDelete;
        }
        catch
        {
            result.IsUsable = false;
        }

        _report = $"temp usable={result.IsUsable}, rwdel=({result.CanRead},{result.CanWrite},{result.CanDelete}), path={result.Directory}";
        services.AddSingleton(result);
        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}

public sealed class TempDirectoryState
{
    public string Directory { get; set; } = "";
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool IsUsable { get; set; }
}
