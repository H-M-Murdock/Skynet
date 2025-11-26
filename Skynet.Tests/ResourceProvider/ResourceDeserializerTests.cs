using Skynet.Core.ResourceProvider;

namespace Skynet.Tests.ResourceProvider;

public class ResourceDeserializerTests
{
    // Test-Implementierung: Ein Deserializer für "int", der nur JSON will
    private class JsonIntDeserializer : ResourceDeserializer<int>
    {
        public override int Priority => 1;
        public override string? ExpectedContentType => "application/json";

        public override bool CanHandle(string logicalKey, string? contentType)
        {
            // Wenn die Basisklasse uns durchlässt, akzeptieren wir alles
            return true;
        }

        public override int Deserialize(ResourceEnvelope envelope) => 123;
    }

    // Test-Implementierung: Ein "Wildcard" Deserializer (kein ContentType Zwang)
    private class AnyIntDeserializer : ResourceDeserializer<int>
    {
        public override int Priority => 2;
        public override string? ExpectedContentType => null; // Nimmt alles

        public override bool CanHandle(string logicalKey, string? contentType) => true;
        public override int Deserialize(ResourceEnvelope envelope) => 456;
    }

    [Fact]
    public void BaseClass_Should_Filter_By_TargetType()
    {
        var d = new JsonIntDeserializer();
        
        // Wir fragen nach "string", Deserializer liefert "int" -> False
        bool result = ((IResourceDeserializer)d).CanHandle(typeof(string), "key", "application/json");
        
        Assert.False(result);
    }

    [Fact]
    public void BaseClass_Should_Filter_By_ContentType_Mismatch()
    {
        var d = new JsonIntDeserializer();

        // Erwartet "application/json", bekommt "text/xml" -> False
        bool result = ((IResourceDeserializer)d).CanHandle(typeof(int), "key", "text/xml");

        Assert.False(result);
    }

    [Fact]
    public void BaseClass_Should_Allow_ContentType_Match()
    {
        var d = new JsonIntDeserializer();

        // Erwartet "application/json", bekommt "application/JSON" (case insensitive) -> True
        bool result = ((IResourceDeserializer)d).CanHandle(typeof(int), "key", "application/JSON");

        Assert.True(result);
    }

    [Fact]
    public void BaseClass_Should_Allow_Any_ContentType_If_Expected_Is_Null()
    {
        var d = new AnyIntDeserializer(); // ExpectedContentType = null

        // Sollte bei XML trotzdem true sein, weil die Basisklasse nicht filtert
        bool result = ((IResourceDeserializer)d).CanHandle(typeof(int), "key", "text/xml");

        Assert.True(result);
    }
}
