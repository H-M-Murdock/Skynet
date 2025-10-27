namespace Skynet.Core.Logging;

public interface ILogEventMaterializer
{
    ILogEvent Materialize(ILogEvent src);
}