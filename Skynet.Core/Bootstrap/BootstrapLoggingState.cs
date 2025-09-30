// Skynet.Core.Bootstrap/BootstrapLoggingState.cs
namespace Skynet.Core.Bootstrap;

public sealed class BootstrapLoggingState
{
    public bool CanWriteFiles { get; set; }
    public string? LogDirectory { get; set; }
}
