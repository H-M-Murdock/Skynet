using Microsoft.Extensions.DependencyInjection;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapResult : IAsyncDisposable
{
    public bool IsSuccess => Exception == null && ServiceProvider != null;
    public IServiceProvider? ServiceProvider { get; }
    public Exception? Exception { get; }
    public BootstrapExitCode ExitCode { get; }
    
    // Wir behalten den Context, um ihn beim Shutdown sauber zu disposen
    private readonly BootstrapContext? _context;

    private BootstrapResult(IServiceProvider? sp, Exception? ex, BootstrapExitCode code, BootstrapContext? context)
    {
        ServiceProvider = sp;
        Exception = ex;
        ExitCode = code;
        _context = context;
    }

    public static BootstrapResult Success(IServiceProvider sp, BootstrapContext context) 
        => new(sp, null, BootstrapExitCode.Success, context);

    public static BootstrapResult Failure(Exception ex, BootstrapExitCode code, BootstrapContext? context) 
        => new(null, ex, code, context);

    public async ValueTask DisposeAsync()
    {
        // Wenn wir gescheitert sind, müssen wir den Context (und damit die Logger) aufräumen.
        // Wenn wir erfolgreich waren, lebt der ServiceProvider weiter (und hat idealerweise die Logger übernommen).
        if (!IsSuccess && _context != null)
        {
            await _context.DisposeAsync();
        }
    }
}
