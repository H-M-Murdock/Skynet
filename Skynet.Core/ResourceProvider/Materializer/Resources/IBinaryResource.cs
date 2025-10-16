// C#

namespace Skynet.Core.ResourceProvider;

public interface IBinaryResource : IRawResource
{
    long? Length { get; }      // falls bekannt (z. B. FileInfo)
    string? Hash { get; }      // optionaler Hash zur Integrität
}