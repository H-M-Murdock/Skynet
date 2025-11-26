using Skynet.Core.Tenant;

namespace Skynet.Tests.tenant;

public class TenantIdTests
{
    [Fact]
    public void Equals_Should_Return_True_For_Same_Guids()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new TenantId(guid);
        var id2 = new TenantId(guid);

        // Assert
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);
        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void New_Should_Generate_Unique_Ids()
    {
        var id1 = TenantId.New();
        var id2 = TenantId.New();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Empty_Should_Be_Guid_Empty()
    {
        Assert.Equal(Guid.Empty, TenantId.Empty.Value);
        Assert.Equal(new TenantId(Guid.Empty), TenantId.Empty);
    }

    [Fact]
    public void Parse_Should_Create_Instance_From_String()
    {
        var guid = Guid.NewGuid();
        var id = TenantId.Parse(guid.ToString());
        
        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void TryParse_Should_Handle_Invalid_Input()
    {
        var result = TenantId.TryParse("keine-guid", out var id);
        
        Assert.False(result);
        Assert.Equal(TenantId.Empty, id);
    }

    [Fact]
    public void CompareTo_Should_Order_Correctly()
    {
        // Arrange
        // Wir konstruieren Guids so, dass guid1 lexikalisch kleiner ist als guid2
        var guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id1 = new TenantId(guid1);
        var id2 = new TenantId(guid2);

        // Act & Assert
        Assert.True(id1 < id2);
        Assert.True(id2 > id1);
        Assert.Equal(-1, id1.CompareTo(id2));
        Assert.Equal(1, id2.CompareTo(id1));
    }

    [Fact]
    public void Implicit_Conversion_To_Guid()
    {
        var guid = Guid.NewGuid();
        TenantId id = new TenantId(guid);
        Guid converted = id; // Implicit

        Assert.Equal(guid, converted);
    }
}
