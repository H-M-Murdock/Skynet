using Moq;
using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

namespace Skynet.Tests.ResourceProvider;

public class ResourceSerializerTests
{
    // Test-Serializer für "int"
    private class IntSerializer : ResourceSerializer<int>
    {
        public override int Priority => 1;
        public override string ContentType => "text/plain";

        public override bool CanHandle(int value, string logicalKey, ITenantContext tenant)
        {
            // Wir akzeptieren nur positive ints
            return value > 0;
        }

        public override async Task SerializeAsync(int value, string logicalKey, ITenantContext tenant, Stream destination, CancellationToken ct = default)
        {
            var bytes = new byte[] { (byte)value }; // Dummy Serialization
            await destination.WriteAsync(bytes, ct);
        }
    }

    private readonly IntSerializer _serializer = new();
    private readonly Mock<ITenantContext> _tenant = new();

    [Fact]
    public void BaseClass_CanHandle_Should_Reject_Wrong_Type()
    {
        // Wir übergeben einen String, erwarten aber int
        bool result = ((IResourceSerializer)_serializer).CanHandle("string", "key", _tenant.Object);
        Assert.False(result);
    }

    [Fact]
    public void BaseClass_CanHandle_Should_Delegate_Correctly()
    {
        // Fall A: int > 0 -> true
        Assert.True(((IResourceSerializer)_serializer).CanHandle(42, "key", _tenant.Object));

        // Fall B: int <= 0 -> false (durch Implementierung)
        Assert.False(((IResourceSerializer)_serializer).CanHandle(-1, "key", _tenant.Object));
    }

    [Fact]
    public async Task BaseClass_SerializeAsync_Should_Cast_And_Call_Generic()
    {
        // Arrange
        var ms = new MemoryStream();

        // Act
        await ((IResourceSerializer)_serializer).SerializeAsync(123, "key", _tenant.Object, ms);

        // Assert
        Assert.Equal(1, ms.Length);
        Assert.Equal(123, ms.ToArray()[0]);
    }
    
    [Fact]
    public async Task BaseClass_SerializeAsync_Should_Throw_On_Wrong_Type()
    {
        // Arrange
        var ms = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCastException>(async () => 
            await ((IResourceSerializer)_serializer).SerializeAsync("wrong", "key", _tenant.Object, ms));
    }
}
