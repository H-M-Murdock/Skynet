// Skynet.Core/Localization/CultureThreadScopeFactory.cs
namespace Skynet.Core.Localization
{
    public class CultureThreadScopeFactory : ICultureThreadScopeFactory
    {
        private readonly ICurrentCultureProvider _provider;
        public CultureThreadScopeFactory(ICurrentCultureProvider provider) => _provider = provider;
        public CultureThreadScope BeginScope() => new CultureThreadScope(_provider);
    }
}