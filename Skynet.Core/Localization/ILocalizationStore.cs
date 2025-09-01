namespace Skynet.Core.Localization
{
    /// <summary>
    /// Simple store for the currently effective culture (e.g., per user or app-wide).
    /// Implementations can later be tenant-aware.
    /// </summary>
    public interface ILocalizationStore
    {
        string? GetCultureName();
        void SetCulture(string cultureName);
        bool TrySetCulture(string cultureName);        // validiert + setzt oder false
    }
}