namespace Skynet.Core.Localization;

/// <summary>
/// Simple store for the currently effective culture (e.g., per user or app-wide).
/// Implementations can later be tenant-aware.
/// </summary>
public interface ILocalizationStore
{
    /// <summary>
    /// Retrieves the ISO language code (e.g., "en-US", "de-DE") of the current culture.
    /// Returns <c>null</c> if no culture has been explicitly set.
    /// </summary>
    /// <returns>The culture string or null.</returns>
    string? GetCultureName();

    /// <summary>
    /// Sets the current culture strictly.
    /// </summary>
    /// <param name="cultureName">The ISO language code to set.</param>
    /// <exception cref="ArgumentException">Thrown if the culture name is invalid or not supported.</exception>
    void SetCulture(string cultureName);

    /// <summary>
    /// Attempts to validate and set the culture.
    /// </summary>
    /// <param name="cultureName">The ISO language code to set.</param>
    /// <returns><c>true</c> if the culture was valid and set; otherwise, <c>false</c>.</returns>
    bool TrySetCulture(string cultureName);
}
