using System;
using System.Threading;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Thread-safe in-memory culture store using AsyncLocal for request-scope isolation.
    /// </summary>
    public class InMemoryLocalizationStore : ILocalizationStore
    {
        private readonly AsyncLocal<string?> _current = new();

        public string? GetCultureName() => _current.Value;

        public void SetCulture(string cultureName)
        {
            _current.Value = cultureName;
        }
        
        public bool TrySetCulture(string cultureName)
        {
            // No validation here; decorator will handle it if used.
            _current.Value = cultureName;
            return true;
        }
        
    }
}