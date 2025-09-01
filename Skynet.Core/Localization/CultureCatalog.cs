// Skynet.Core/Localization/CultureCatalog.cs
using System.Globalization;
namespace Skynet.Core.Localization
{
    internal static class CultureCatalog
    {
        // Case-insensitive, einmalig aufgebaut
        public static readonly HashSet<string> Known = new(
            CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase);
    }
}