// Skynet.Core/Localization/ICultureThreadScopeFactory.cs
using System;
using System.Globalization;

namespace Skynet.Core.Localization;

public interface ICultureThreadScopeFactory
{
    /// <summary>
    /// Creates a scope using the default provider-derived culture.
    /// </summary>
    IDisposable BeginScope();

    /// <summary>
    /// Creates a scope using a specific culture (explicit override).
    /// </summary>
    IDisposable BeginScope(CultureInfo overrideCulture);
}