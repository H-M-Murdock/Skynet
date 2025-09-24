using System.Globalization;

namespace Skynet.Core.Localization;

public sealed class CultureThreadScopeFactory : ICultureThreadScopeFactory
{
    private sealed class OverrideProvider : ICurrentCultureProvider
    {
        private readonly CultureInfo _culture;
        public OverrideProvider(CultureInfo culture) => _culture = culture;
        public CultureInfo GetCulture() => _culture;
    }

    private readonly ICurrentCultureProvider _provider;

    public CultureThreadScopeFactory(ICurrentCultureProvider provider)
        => _provider = provider;

    public IDisposable BeginScope() => new CultureThreadScope(_provider);

    public IDisposable BeginScope(CultureInfo overrideCulture)
        => new CultureThreadScope(new OverrideProvider(overrideCulture));
}