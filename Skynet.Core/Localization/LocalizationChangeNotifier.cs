using Microsoft.Extensions.Primitives;
using Skynet.Core.Tenant; // Assuming TenantId is here
using System;

namespace Skynet.Core.Localization
{
    /// <summary>
    /// Provides a mechanism to notify and subscribe to culture change events.
    /// Useful for invalidating caches or updating UI components when the localization context changes.
    /// </summary>
    public interface ILocalizationChangeNotifier
    {
        /// <summary>
        /// Gets a change token that triggers when the culture for a specific tenant changes.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant to watch.</param>
        /// <returns>An <see cref="IChangeToken"/> that is signaled on change.</returns>
        IChangeToken WatchTenantCulture(TenantId tenantId);

        /// <summary>
        /// Gets a global change token that triggers when any culture configuration changes across the system.
        /// </summary>
        /// <returns>An <see cref="IChangeToken"/> that is signaled on any change.</returns>
        IChangeToken WatchAnyCulture();

        /// <summary>
        /// Triggers the change token for a specific tenant. 
        /// Should be called by the localization store or admin services after updating settings.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant whose culture changed.</param>
        void NotifyTenantCultureChanged(TenantId tenantId);
    }
}