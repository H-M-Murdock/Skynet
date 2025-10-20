// Skynet.Core.Tenant/TenantContextAccessor.cs
using System.Threading;

namespace Skynet.Core.Tenant;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<Holder> _current = new();

    public ITenantContext? Current
    {
        get => _current.Value?.Context;
        set
        {
            var holder = _current.Value;
            if (holder is not null)
            {
                holder.Context = null;
            }
            if (value is not null)
            {
                _current.Value = new Holder { Context = value };
            }
        }
    }

    public IDisposable BeginScope(ITenantContext context)
    {
        var prior = Current;
        Current = context;
        return new PopWhenDisposed(this, prior);
    }

    private sealed class Holder
    {
        public ITenantContext? Context;
    }

    private sealed class PopWhenDisposed : IDisposable
    {
        private readonly TenantContextAccessor _accessor;
        private readonly ITenantContext? _previous;
        private bool _disposed;

        public PopWhenDisposed(TenantContextAccessor accessor, ITenantContext? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _accessor.Current = _previous;
        }
    }
}