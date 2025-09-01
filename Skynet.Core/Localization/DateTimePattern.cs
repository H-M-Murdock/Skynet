namespace Skynet.Core.Localization
{
    /// <summary>
    /// Well-known pattern aliases. Map to standard .NET format strings.
    /// </summary>
    public static class DateTimePattern
    {
        public const string ShortDate = "d";
        public const string LongDate = "D";
        public const string ShortTime = "t";
        public const string LongTime = "T";
        public const string ShortDateTime = "g";
        public const string LongDateTime = "F"; // or 'G' for long date+short time
    }
}