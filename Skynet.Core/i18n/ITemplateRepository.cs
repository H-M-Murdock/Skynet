using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Core.i18n;

/// <summary>
/// Niedrigste Speicherabstraktion für lokalisierte Templates (ohne Fallback-Logik).
/// Aufgaben:
/// - Lesen/Schreiben konkreter Varianten (TenantScope × Culture × Key).
/// - Aufzählung für Export/Backoffice.
/// Hinweise:
/// - Fallback (Tenant-Chain/Kultur) gehört NICHT hierher; das machen höhere Services.
/// - Version/ETag-Handling erfolgt über I18NTemplate.Version (optimistisches Locking optional).
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Lädt eine konkrete Variante (ohne Fallback).
    /// </summary>
    Task<I18NTemplate?> TryGetExactAsync(
        string tenantScope,
        I18NKey key,
        CultureInfo culture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt eine Variante an oder überschreibt sie. expectedVersion ermöglicht ETag/Versions-Prüfung.
    /// </summary>
    Task UpsertAsync(
        string tenantScope,
        I18NKey key,
        CultureInfo culture,
        string template,
        string? contentType = null,
        string? expectedVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Listet vorhandene Varianten eines Tenant-Scope (für Export/Backoffice).
    /// Optional filterbar nach Domain und Kultur.
    /// </summary>
    Task<IReadOnlyList<I18NEntryId>> ListEntriesAsync(
        string tenantScope,
        string? domain = null,
        CultureInfo? culture = null,
        CancellationToken cancellationToken = default);
}
