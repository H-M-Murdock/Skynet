using Skynet.Core.ResourceProvider;

namespace Skynet.Tests.ResourceProvider;

public class ProviderIdTests
{
    [Fact]
    public void Equality_Should_Work_Correctly()
    {
        var guid = Guid.NewGuid();
        var id1 = new ProviderId(guid);
        var id2 = new ProviderId(guid); // Gleiche GUID
        var id3 = ProviderId.New();     // Andere GUID

        // Value Semantics prüfen
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.False(id1 != id2);

        Assert.NotEqual(id1, id3);
        Assert.True(id1 != id3);
    }

    [Fact]
    public void Dictionary_Usage_Should_Work()
    {
        // Testet GetHashCode implizit
        var dict = new Dictionary<ProviderId, string>();
        var id = ProviderId.New();

        dict[id] = "Test";

        // Muss über Value-Gleichheit gefunden werden, nicht Referenz
        var idCopy = new ProviderId(id.Value); 
        
        Assert.True(dict.ContainsKey(idCopy));
        Assert.Equal("Test", dict[idCopy]);
    }

    [Fact]
    public void Parsing_Should_Handle_Valid_And_Invalid_Inputs()
    {
        var guid = Guid.NewGuid();
        var validString = guid.ToString();

        // Parse
        var id = ProviderId.Parse(validString);
        Assert.Equal(guid, id.Value);

        // TryParse Success
        Assert.True(ProviderId.TryParse(validString, out var res1));
        Assert.Equal(guid, res1.Value);

        // TryParse Failure
        Assert.False(ProviderId.TryParse("invalid-guid", out var res2));
        Assert.Equal(ProviderId.Empty, res2); // Sollte Empty sein bei Fehler
        
        Assert.False(ProviderId.TryParse(null, out _));
    }

    [Fact]
    public void Comparison_Should_Allow_Sorting()
    {
        // Arrange
        var idSmall = new ProviderId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var idBig   = new ProviderId(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"));

        // Act & Assert
        Assert.True(idSmall < idBig);
        Assert.True(idBig > idSmall);
        
        var list = new[] { idBig, idSmall };
        Array.Sort(list);
        
        Assert.Equal(idSmall, list[0]);
        Assert.Equal(idBig, list[1]);
    }
}
