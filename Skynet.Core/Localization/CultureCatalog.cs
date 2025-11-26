// Skynet.Core/Localization/CultureCatalog.cs
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Internal cache of all known culture names available on the current system.
    /// Used for high-performance validation of culture strings without throwing exceptions.
    /// </summary>
    internal static class CultureCatalog
    {
        /// <summary>
        /// A case-insensitive set of all system culture names (e.g. "en-US", "de", "").
        /// Initialized lazily on first access.
        /// </summary>
        public static readonly HashSet<string> Known = new(
            CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase);
    }
}