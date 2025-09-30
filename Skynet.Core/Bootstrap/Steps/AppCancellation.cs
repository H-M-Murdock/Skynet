namespace Skynet.Core.Bootstrap.Steps;

public sealed class AppCancellation(CancellationToken token)
{
    public CancellationToken Token { get; } = token;
}