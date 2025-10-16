namespace Skynet.Core.Logging;

public interface IEventTransportFactory
{
    IEventTransport Create();
}