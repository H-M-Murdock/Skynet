// File: Skynet.Core/Time/IClock.cs

namespace Skynet.Core.Time
{
    /// <summary>
    /// Abstraction of the current time for testable, DI-friendly code.
    /// </summary>
    public interface IClock
    {
        /// <summary>Gets the current UTC time.</summary>
        DateTime UtcNow { get; }

        /// <summary>Gets today's date based on UtcNow.</summary>
        DateOnly Today => DateOnly.FromDateTime(UtcNow);
    }
}