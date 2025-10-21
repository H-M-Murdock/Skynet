using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class FileLogSinkTests
{
    private static MutableLogEvent NewEvt()
        => new()
        {
            Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
            Level = LogLevel.Information,
            EventId = new EventId(1, "E"),
            GlobalEventId = "gid",
            CategoryName = "Cat",
            Operation = "Op",
            State = new List<KeyValuePair<string, object?>> { new("k", "v") }
        };

    [Fact]
    public async Task Writes_Line_To_File()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "skynet_tests_filelogsink_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var services = new ServiceCollection();
        services.AddSingleton<ILogTextFormatter>(new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: true));
        services.AddSingleton<IPathTemplateRenderer, DefaultPathTemplateRenderer>();
        services.AddOptions<FileLogSinkOptions>().Configure(o =>
        {
            o.BaseRoot = tmp;
            o.Tenant = "t1";
            o.SubFolder = "app";
            o.PathTemplate = "logs/{tenant}/{component}/{yyyy-MM-dd}.log"; // component fehlt -> fällt weg
            o.BufferFlushBytes = 0; // nur expliziter Flush
            o.FlushIntervalMs = 0;
        });

        var sp = services.BuildServiceProvider();
        var sink = new FileLogSink(
            sp.GetRequiredService<ILogTextFormatter>(),
            sp.GetRequiredService<IPathTemplateRenderer>(),
            sp.GetRequiredService<IOptionsMonitor<FileLogSinkOptions>>());

        await sink.StartAsync(CancellationToken.None);

        var e = NewEvt();
        await sink.WriteAsync(e, CancellationToken.None);
        await sink.FlushAsync(CancellationToken.None);
        await sink.DisposeAsync(); // sicherstellen, dass alles geschlossen ist

        var full = Path.Combine(tmp, "t1", "app", "logs", "2024-12-31.log");
        Assert.True(File.Exists(full));
        var text = await File.ReadAllTextAsync(full);
        Assert.Contains("2024-12-31T23:59:58.0000000Z Information", text);

        await sink.DisposeAsync();
        Directory.Delete(tmp, recursive: true);
    }

    [Fact]
    public async Task Timer_Flush_Writes_Without_Explicit_Flush()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "skynet_tests_filelogsink_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var formatter = new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: false);
        var renderer = new DefaultPathTemplateRenderer();
        var opts = Options.Create(new FileLogSinkOptions
        {
            BaseRoot = tmp,
            Tenant = "t2",
            PathTemplate = "{yyyy-MM-dd}.log",
            BufferFlushBytes = 0,
            FlushIntervalMs = 100
        });

        var sink = new FileLogSink(formatter, renderer, new OptionsMonitorFake<FileLogSinkOptions>(opts.Value));
        await sink.StartAsync(CancellationToken.None);

        await sink.WriteAsync(NewEvt(), CancellationToken.None);
        await Task.Delay(250); // warte auf Timer
        var full = Path.Combine(tmp, "t2", "2024-12-31.log");
        Assert.True(File.Exists(full));

        await sink.DisposeAsync();
        Directory.Delete(tmp, recursive: true);
    }

    private sealed class OptionsMonitorFake<T> : IOptionsMonitor<T> where T : class, new()
    {
        private T _current;
        private event Action<T, string>? _onChange;

        public OptionsMonitorFake(T current) => _current = current;

        public T CurrentValue => _current;

        public T Get(string? name) => _current;

        public IDisposable OnChange(Action<T, string> listener)
        {
            _onChange += listener;
            return new Unsub(() => _onChange -= listener);
        }

        public void Update(T value)
        {
            _current = value;
            _onChange?.Invoke(value, string.Empty);
        }

        private sealed class Unsub : IDisposable
        {
            private readonly Action _dispose;
            public Unsub(Action d) => _dispose = d;
            public void Dispose() => _dispose();
        }
    }

    [Fact]
    public async Task Rotation_Triggers_On_MaxFileBytes()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "skynet_tests_filelogsink_rot_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var formatter = new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: false);
        var renderer = new DefaultPathTemplateRenderer();
        var opts = Options.Create(new FileLogSinkOptions
        {
            BaseRoot = tmp,
            Tenant = "t3",
            PathTemplate = "{yyyy-MM-dd}.log",
            BufferFlushBytes = 0,
            FlushIntervalMs = 0,
            MaxFileBytes = 30, // klein, um Rotation auszulösen
            WriteThrough = true
        });

        var sink = new FileLogSink(formatter, renderer, new OptionsMonitorFake<FileLogSinkOptions>(opts.Value));
        await sink.StartAsync(CancellationToken.None);

        // Zwei Zeilen schreiben; zusammen sollten sie > MaxFileBytes sein -> Rotation
        await sink.WriteAsync(NewEvt(), CancellationToken.None);
        await sink.FlushAsync(CancellationToken.None);
        await sink.WriteAsync(NewEvt(), CancellationToken.None);
        await sink.FlushAsync(CancellationToken.None);
        await sink.DisposeAsync();

        var baseFile = Path.Combine(tmp, "t3", "2024-12-31.log");
        Assert.True(File.Exists(baseFile));
        // Erwartet: mindestens eine Rotationsdatei
        var rotated = baseFile + ".1";
        Assert.True(File.Exists(rotated));

        Directory.Delete(tmp, recursive: true);
    }
    
}
