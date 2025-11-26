using System.Threading;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// A thread-safe, in-memory implementation of <see cref="ILocalizationStore"/> using <see cref="AsyncLocal{T}"/>.
    /// This store isolates the culture context to the current asynchronous control flow (e.g. a single HTTP request).
    /// Ideally used wrapped by <see cref="ValidatedLocalizationStore"/> to ensure data integrity.
    /// </summary>
    public class InMemoryLocalizationStore : ILocalizationStore
    {
        private readonly AsyncLocal<string?> _current = new();

        /// <inheritdoc />
        public string? GetCultureName() => _current.Value;

        /// <inheritdoc />
        public void SetCulture(string cultureName)
        {
            // This raw implementation accepts any string.
            // Validation is expected to be handled by a decorator or caller.
            _current.Value = cultureName;
        }

        /// <inheritdoc />
        public bool TrySetCulture(string cultureName)
        {
            // Accepts everything blindly. 
            // Returns true to indicate "storage successful", not necessarily "value valid".
            _current.Value = cultureName;
            return true;
        }
    }
}