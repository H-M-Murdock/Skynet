using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

public sealed record ResourceRequest(
    TenantId TenantId,
    string Key,
    ResourceKind ResourceType
);