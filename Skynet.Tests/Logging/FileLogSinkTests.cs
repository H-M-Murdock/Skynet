using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skynet.Core.Logging;
using Xunit;

namespace Skynet.Tests.Logging;

public sealed class FileLogSinkParallelTests
{
    private static MutableLogEvent NewEvt(int i) => new()
    {
        Timestamp = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero),
        Level = LogLevel.Information,
        EventId = new EventId(i),
        GlobalEventId = i.ToString("n"),
        CategoryName = "Cat.Sub",
        State = new List<KeyValuePair<string, object?>>()
    };

    [Fact]
    public async Task Parallel_Writes_Produce_All_Lines()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "skynet_tests_filelogsink_par_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var services = new ServiceCollection();
        services.AddSingleton<ILogTextFormatter>(new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: false));
        services.AddSingleton<IPathTemplateRenderer, DefaultPathTemplateRenderer>();
        services.AddOptions<FileLogSinkOptions>().Configure(o =>
        {
            o.BaseRoot = tmp;
            o.Tenant = "tpar";
            o.PathTemplate = "{yyyy-MM-dd}.log";
            o.BufferFlushBytes = 0;
            o.FlushIntervalMs = 0;
            o.WriteThrough = true;
        });

        var sp = services.BuildServiceProvider();
        var sink = new FileLogSink(
            sp.GetRequiredService<ILogTextFormatter>(),
            sp.GetRequiredService<IPathTemplateRenderer>(),
            sp.GetRequiredService<IOptionsMonitor<FileLogSinkOptions>>());

        await sink.StartAsync(CancellationToken.None);

        var tasks = new List<Task>();
        for (int t = 0; t < 5; t++)
        {
            var tt = t;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                    await sink.WriteAsync(NewEvt(tt * 100 + i), CancellationToken.None);
            }));
        }
        await Task.WhenAll(tasks);
        await sink.FlushAsync(CancellationToken.None);
        await sink.DisposeAsync();

        var full = Path.Combine(tmp, "tpar", "2024-12-31.log");
        Assert.True(File.Exists(full));
        var lines = await File.ReadAllLinesAsync(full);
        Assert.True(lines.Length >= 500);

        Directory.Delete(tmp, recursive: true);
    }

    [Fact]
    public async Task Template_With_Subfolders_Writes_Correct_Path()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "skynet_tests_filelogsink_tpl_" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tmp);

        var formatter = new SimpleLineLogTextFormatter(useUtcTimestamps: true, includeState: false);
        var renderer = new DefaultPathTemplateRenderer();
        var opts = Options.Create(new FileLogSinkOptions
        {
            BaseRoot = tmp,
            Tenant = "tcat",
            PathTemplate = "{category}/{yyyy-MM-dd}.log",
            BufferFlushBytes = 0,
            FlushIntervalMs = 0,
            WriteThrough = true
        });

        var sink = new FileLogSink(formatter, renderer, new OptionsMonitorFake<FileLogSinkOptions>(opts.Value));
        await sink.StartAsync(CancellationToken.None);

        var e = NewEvt(1);
        await sink.WriteAsync(e, CancellationToken.None);
        await sink.FlushAsync(CancellationToken.None);
        await sink.DisposeAsync();

        var full = Path.Combine(tmp, "tcat", "Cat.Sub", "2024-12-31.log");
        Assert.True(File.Exists(full));

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
}
