namespace Skynet.Core.ResourceProvider;
using System;
using System.IO;

public interface IResourceProvider
{
    /// <summary>Fast, synchronous capability check (avoid I/O).</summary>
    bool CanHandle(ResourceRequest request);

    /// <summary>Asynchronously tries to resolve a resource for the given request.</summary>
    Task<(bool found, IResourceResult? result)> TryGetAsync(
        ResourceRequest request,
        CancellationToken cancellationToken = default);
}