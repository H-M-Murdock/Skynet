// C#
// Skynet.Core.ResourceProvider/IJsonResource.cs
using System.Text.Json.Nodes;

namespace Skynet.Core.ResourceProvider;

public interface IJsonResource : IStringResource
{
    JsonNode Node { get; } // System.Text.Json DOM (mutable)
}
