using Skynet.Core.Time;

namespace Skynet.Core.Localization
{
    public interface IDateTimeFormatter
    {
        string Format(IClock value, string patternAliasOrCustom);
    }
}