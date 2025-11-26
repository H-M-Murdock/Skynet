// Skynet.Core/Localization/CultureThreadScope.cs
using System;
using System.Globalization;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Applies the effective culture to CurrentCulture & CurrentUICulture for the lifetime of this scope.
    /// Restores previous values on dispose. Safe for nested scopes.
    /// Internal implementation detail of <see cref="CultureThreadScopeFactory"/>.
    /// </summary>
    internal sealed class CultureThreadScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;
        private bool _disposed;

        /// <summary>
        /// Initializes a new scope with the specified target culture.
        /// Captures the current thread culture state to restore it later.
        /// </summary>
        /// <param name="targetCulture">The culture to apply for this scope.</param>
        public CultureThreadScope(CultureInfo targetCulture)
        {
            if (targetCulture == null) throw new ArgumentNullException(nameof(targetCulture));

            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;

            CultureInfo.CurrentCulture = targetCulture;
            CultureInfo.CurrentUICulture = targetCulture;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
            
            _disposed = true;
        }
    }
}