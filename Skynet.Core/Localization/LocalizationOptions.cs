namespace Skynet.Core.Localization
{
    public class LocalizationOptions
    {
        public string DefaultCulture { get; set; } = "en-US";
        public string DefaultUICulture { get; set; } = "en-US";
        public IReadOnlyCollection<string>? SupportedCultures { get; set; } // null => alle erlauben
    }
}