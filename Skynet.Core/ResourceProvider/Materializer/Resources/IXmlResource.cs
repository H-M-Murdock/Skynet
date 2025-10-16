// C#
// Skynet.Core.ResourceProvider/IXmlResource.cs
using System.Xml.Linq;

namespace Skynet.Core.ResourceProvider;

public interface IXmlResource : IStringResource
{
    XDocument Document { get; } // LINQ to XML
}
