// Skynet.Core/Localization/CultureThreadScope.cs
using System;
using System.Globalization;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Applies the effective culture to CurrentCulture & CurrentUICulture for the lifetime of this scope.
    /// Restores previous values on dispose. Safe for nested scopes.
    /// </summary>
    public sealed class CultureThreadScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUICulture;
        private bool _disposed;

        public CultureThreadScope(ICurrentCultureProvider provider)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUICulture = CultureInfo.CurrentUICulture;

            var effective = provider.GetCulture();
            CultureInfo.CurrentCulture = effective;
            CultureInfo.CurrentUICulture = effective;
        }

        public void Dispose()
        {
            if (_disposed) return;
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUICulture;
            _disposed = true;
        }
    }
}