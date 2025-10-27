using System;

namespace Skynet.Core.Logging;

public sealed class InMemoryEventTransportFactory : IEventTransportFactory
{
    private readonly InMemoryEventListener _listener;
    private readonly int _defaultCapacity;

    /// <summary>
    /// Creates an in-memory transport bound to the given <see cref="InMemoryEventListener"/>.
    /// Only for local, in-process scenarios.
    /// </summary>
    public InMemoryEventTransportFactory(
        InMemoryEventListener listener,
        int defaultCapacity = 1024)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _defaultCapacity = Math.Max(1, defaultCapacity);
    }

    /// <summary>
    /// Returns a new transport instance connected to the shared in-memory listener.
    /// </summary>
    public IEventTransport Create()
        => new InMemoryEventTransport(_listener, _defaultCapacity);
}