using System.Globalization;

namespace Skynet.Core.Localization
{
    public interface ICurrentCultureProvider
    {
        CultureInfo GetCulture();
    }
}