namespace Skynet.Core.Bootstrap;

public sealed class AppCancellation(CancellationToken token)
{
    public CancellationToken Token { get; } = token;
}