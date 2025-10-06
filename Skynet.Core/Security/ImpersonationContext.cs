// Skynet.Core/Security/ImpersonationContext.cs
using System;
using System.Runtime.InteropServices;

#if WINDOWS
using System.Security.Principal;
#endif

namespace Skynet.Core.Security;

public sealed class ImpersonationContext : IDisposable, IAsyncDisposable
{
#if WINDOWS
    private readonly WindowsImpersonationContext? _winCtx;
    private ImpersonationContext(WindowsImpersonationContext? ctx) => _winCtx = ctx;
#else
    private ImpersonationContext() { }
#endif

    public static ImpersonationContext BeginWithToken(IntPtr userToken)
    {
#if WINDOWS
        if (userToken == IntPtr.Zero) throw new ArgumentException("Token must not be zero.", nameof(userToken));
        using var id = new WindowsIdentity(userToken);
        var ctx = id.Impersonate();
        return new ImpersonationContext(ctx);
#else
        return new ImpersonationContext(); // No-Op auf Nicht-Windows
#endif
    }

    public static ImpersonationContext BeginWithIdentity(object identity)
    {
#if WINDOWS
        if (identity is not WindowsIdentity wi)
            throw new ArgumentException("Requires WindowsIdentity on Windows.", nameof(identity));
        var ctx = wi.Impersonate();
        return new ImpersonationContext(ctx);
#else
        return new ImpersonationContext(); // No-Op auf Nicht-Windows
#endif
    }

    public void Dispose()
    {
#if WINDOWS
        _winCtx?.Undo();
        _winCtx?.Dispose();
#endif
    }

    public async ValueTask DisposeAsync()
    {
        // Falls zukünftig asynchrone Aufräumarbeiten nötig sind, hier awaiten.
        Dispose();
        await Task.CompletedTask;
    }

    public static bool IsSupported =>
#if WINDOWS
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
        false;
#endif
}