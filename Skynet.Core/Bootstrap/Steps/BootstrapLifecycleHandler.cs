// Skynet.Core.Bootstrap/Steps/BootstrapLifecycleStep.cs

using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapLifecycleStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private static bool _installed;
    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        var cts = new CancellationTokenSource();

        if (!_installed)
        {
            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _installed = true;
        }

        // Registriere die Source (optional, falls jemand sie direkt braucht)
        services.AddSingleton(cts);
        // Registriere einen referenztypischen Wrapper, damit DI ihn auflösen kann
        services.AddSingleton(new AppCancellation(cts.Token));

        _report = "lifecycle hooks installed (Ctrl+C, ProcessExit, exception hooks).";
        return Task.CompletedTask;

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            SafeWrite("[LIFECYCLE] Cancel requested (Ctrl+C).");
            TryCancel(cts);
        }

        void OnProcessExit(object? sender, EventArgs e)
        {
            SafeWrite("[LIFECYCLE] ProcessExit requested.");
            TryCancel(cts);
        }

        void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            SafeWrite("[FATAL] UnhandledException: " + (e.ExceptionObject?.ToString() ?? "<null>"));
            FlushFallback();
        }

        void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            SafeWrite("[FATAL] UnobservedTaskException: " + e.Exception);
            e.SetObserved();
            FlushFallback();
        }
    }

    public string GetReport() => _report;

    private static void TryCancel(CancellationTokenSource cts)
    {
        try { if (!cts.IsCancellationRequested) cts.Cancel(); } catch { }
    }

    private static void SafeWrite(string line)
    {
        try
        {
            var ts = DateTime.UtcNow.ToString("O");
            Console.Error.WriteLine($"[{ts}] {line}");

            var dir = "./bootstrap";
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "lifecycle.log");
            File.AppendAllLines(path, new[] { $"[{ts}] {line}" });
        }
        catch { }
    }

    private static void FlushFallback()
    {
        try { Console.Error.Flush(); } catch { }
    }
}