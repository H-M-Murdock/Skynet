namespace Skynet.Core.Logging;

/// <summary>
/// Konfiguration für RedactionPolicy.
/// </summary>
public sealed class RedactionOptions
{
    public List<string> Keys { get; set; } = new();
    public List<string> Prefixes { get; set; } = new();
    public List<string> Suffixes { get; set; } = new();
    public List<string> RegexPatterns { get; set; } = new();
    public string Mask { get; set; } = "***";
}