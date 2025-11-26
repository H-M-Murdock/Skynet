using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Core.i18n;

/// <summary>
/// In-Memory Implementierung für Tests und schnelles Prototyping.
/// </summary>
public sealed class InMemoryTemplateRepository : ITemplateRepository
{
    // Key: "Scope|Culture|Domain:Name"
    private readonly ConcurrentDictionary<string, I18NTemplate> _store = new();

    public Task<I18NTemplate?> TryGetExactAsync(string tenantScope, I18NKey key, CultureInfo culture, CancellationToken cancellationToken = default)
    {
        var storeKey = BuildStoreKey(tenantScope, key, culture);
        if (_store.TryGetValue(storeKey, out var template))
        {
            return Task.FromResult<I18NTemplate?>(template);
        }
        return Task.FromResult<I18NTemplate?>(null);
    }

    public Task UpsertAsync(string tenantScope, I18NKey key, CultureInfo culture, string template, string? contentType = null, string? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        var storeKey = BuildStoreKey(tenantScope, key, culture);

        _store.AddOrUpdate(storeKey,
            // Add
            _ => new I18NTemplate
            {
                Key = key,
                Template = template,
                ContentType = contentType,
                Source = tenantScope,
                Version = Guid.NewGuid().ToString("N"),
                LastModified = DateTimeOffset.UtcNow
            },
            // Update
            (_, existing) =>
            {
                if (expectedVersion != null && existing.Version != expectedVersion)
                {
                    throw new InvalidOperationException($"Concurrency conflict. Expected: {expectedVersion}, Actual: {existing.Version}");
                }

                return existing.WithUpdate(
                    template, 
                    Guid.NewGuid().ToString("N"), 
                    DateTimeOffset.UtcNow) with { ContentType = contentType ?? existing.ContentType };
            });

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string tenantScope, I18NKey key, CultureInfo culture, string? expectedVersion = null, CancellationToken cancellationToken = default)
    {
        var storeKey = BuildStoreKey(tenantScope, key, culture);
        
        if (_store.TryGetValue(storeKey, out var existing))
        {
            if (expectedVersion != null && existing.Version != expectedVersion)
                 throw new InvalidOperationException($"Concurrency conflict. Expected: {expectedVersion}, Actual: {existing.Version}");
            
            return Task.FromResult(((ICollection<KeyValuePair<string, I18NTemplate>>)_store).Remove(new KeyValuePair<string, I18NTemplate>(storeKey, existing)));
        }
        
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<I18NEntryId>> ListEntriesAsync(string tenantScope, string? domain = null, CultureInfo? culture = null, CancellationToken cancellationToken = default)
    {
        var query = _store.Values
            .Where(t => t.Source == tenantScope); // In diesem Simple-Repo nutzen wir Source als Scope-Marker

        if (!string.IsNullOrEmpty(domain))
        {
            query = query.Where(t => t.Key.Domain == domain);
        }

        // Da wir Culture nicht im Template speichern (außer implizit), müssen wir den Store-Key parsen oder
        // in einer echten Implementierung besser indizieren.
        // Für Testzwecke hier vereinfacht: Wir iterieren über Keys.
        
        var result = new List<I18NEntryId>();
        foreach (var kvp in _store)
        {
            // Re-Parse key to filter
            var parts = kvp.Key.Split('|');
            var scope = parts[0];
            var cultureName = parts[1];
            // var keyStr = parts[2];

            if (scope != tenantScope) continue;
            if (culture != null && !culture.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase)) continue;

            var t = kvp.Value;
            if (!string.IsNullOrEmpty(domain) && t.Key.Domain != domain) continue;

            result.Add(new I18NEntryId(Guid.NewGuid(), t.Key, scope, cultureName));
        }

        return Task.FromResult<IReadOnlyList<I18NEntryId>>(result);
    }

    private static string BuildStoreKey(string scope, I18NKey key, CultureInfo culture)
    {
        return $"{scope}|{culture.Name}|{key}";
    }
}
