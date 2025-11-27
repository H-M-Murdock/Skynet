using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Skynet.Core.Logging;

namespace Skynet.Core.Bootstrap;

public class RegisterLoggingStackStep : IBootStep
{
    public string Name => "Register Full Logging Stack";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        var services = context.Services;

        // 1. & 2. Basis & Transport (wie gehabt) ...
        // ... (Formatter, Encoders, Policies, InMemoryTransportFactory) ...
        services.TryAddSingleton<ILogTextFormatter, SimpleLineLogTextFormatter>();
        services.TryAddSingleton<ILogEventEncoder, NdjsonLogEventEncoder>(); 
        services.TryAddSingleton<ILogEventMaterializer, DefaultLogEventMaterializer>();
        services.TryAddSingleton<IPathTemplateRenderer, DefaultPathTemplateRenderer>();
        services.TryAddSingleton<IBackpressurePolicy>(new SimpleBackpressurePolicy());
        services.TryAddSingleton<IEnricher, DefaultEnricher>();
        services.TryAddSingleton<ISizeLimiter, SizeLimiter>();
        
        // InMemory Setup (für Local)
        services.TryAddSingleton<InMemoryEventListener>(); 
        services.TryAddSingleton<IEventListener>(sp => sp.GetRequiredService<InMemoryEventListener>());
        services.TryAddSingleton<IEventTransportFactory>(sp => 
            new InMemoryEventTransportFactory(sp.GetRequiredService<InMemoryEventListener>()));


        // --- CLIENT SEITE ---
        services.TryAddSingleton<ILoggingClient, LoggingClient>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, LoggerProvider>());
        
        // NEU: Der Client bekommt seinen eigenen "Motor"
        services.AddHostedService<LoggingClientService>();


        // --- SERVER SEITE ---
        // später prüfen: "Bin ich Server?"
        // context.GetItem<bool>("IsLogServer")
        
        services.TryAddSingleton<FileLogSink>();
        services.TryAddSingleton<ILogRouter>(sp => new SingleSinkRouter(sp.GetRequiredService<FileLogSink>()));
        services.TryAddSingleton<ILoggingServer, LoggingServer>();

        // Config für FileSink
        if (context.Items.TryGetValue("Path:Root", out var rootObj) && rootObj is string rootPath)
        {
            services.Configure<FileLogSinkOptions>(opt => 
            {
                opt.BaseRoot = rootPath;
                opt.Tenant = "system"; 
                opt.PathTemplate = "logs/{yyyy-MM-dd}/{level}.log";
            });
        }

        // NEU: Der Server bekommt seinen eigenen "Motor"
        services.AddHostedService<LoggingServerService>();

        return Task.FromResult("Registered: Split Client & Server Stack");
    }
}