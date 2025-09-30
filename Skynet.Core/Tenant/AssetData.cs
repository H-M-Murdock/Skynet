namespace Skynet.Core.Tenant;

public sealed record AssetData(Stream Stream, string? ContentType = null, string? FileName = null);