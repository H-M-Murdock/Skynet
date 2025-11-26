// C#
// Skynet.Core.ResourceProvider/IXmlResource.cs
using System.Xml.Linq;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Eine bereits geparste XML-Ressource (LINQ to XML).
/// </summary>
public interface IXmlResource : IStringResource
{
    /// <summary>Das vollständige XML-Dokument.</summary>
    XDocument Document { get; }
}
