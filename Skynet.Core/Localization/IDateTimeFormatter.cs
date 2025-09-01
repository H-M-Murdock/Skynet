using System;

namespace Skynet.Core.Localization
{
    public interface IDateTimeFormatter
    {
        string Format(DateTime value, string patternAliasOrCustom);
    }
}