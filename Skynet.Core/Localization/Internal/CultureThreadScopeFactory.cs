using System;
using System.Globalization;
using System.Threading;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Standard implementation of the scope factory.
    /// Manages the lifecycle of culture changes on the current thread.
    /// </summary>
    public sealed class CultureThreadScopeFactory : ICultureThreadScopeFactory
    {
        private readonly ICurrentCultureProvider _provider;

        public CultureThreadScopeFactory(ICurrentCultureProvider provider)
            => _provider = provider;

        public IDisposable BeginScope()
        {
            // Factory löst Provider auf und übergibt die Info
            return new CultureThreadScope(_provider.GetCulture());
        }

        public IDisposable BeginScope(CultureInfo overrideCulture)
        {
            // Factory übergibt Override direkt
            return new CultureThreadScope(overrideCulture);
        }
    }
}