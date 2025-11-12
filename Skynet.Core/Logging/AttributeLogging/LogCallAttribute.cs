using Microsoft.Extensions.Logging;

namespace Skynet.Core.Logging;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
public sealed class LogCallAttribute : Attribute
{
    public LogLevel LevelOnEnter { get; init; } = LogLevel.Debug;
    public LogLevel LevelOnExit  { get; init; } = LogLevel.Debug;
    public LogLevel LevelOnException { get; init; } = LogLevel.Error;

    public bool LogParameters { get; init; } = true;
    public bool LogReturnValue { get; init; } = false;
}