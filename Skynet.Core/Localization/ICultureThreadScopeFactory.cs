// Skynet.Core/Localization/ICultureThreadScopeFactory.cs
using System;
using System.Globalization;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Factory for creating disposable scopes that temporarily modify the current thread's culture.
    /// Essential for background workers or console applications to enforce a specific culture context.
    /// </summary>
    public interface ICultureThreadScopeFactory
    {
        /// <summary>
        /// Creates a scope using the culture from the current <see cref="ICurrentCultureProvider"/>.
        /// Upon disposal, the original thread culture is restored.
        /// </summary>
        /// <returns>A disposable token that restores the previous culture when disposed.</returns>
        IDisposable BeginScope();

        /// <summary>
        /// Creates a scope using a specific culture override.
        /// Sets both <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/>.
        /// Upon disposal, the original thread culture is restored.
        /// </summary>
        /// <param name="overrideCulture">The specific culture to apply within this scope.</param>
        /// <returns>A disposable token that restores the previous culture when disposed.</returns>
        IDisposable BeginScope(CultureInfo overrideCulture);
    }
}