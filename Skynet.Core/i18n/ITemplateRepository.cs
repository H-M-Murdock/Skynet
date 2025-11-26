using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Skynet.Core.i18n;

/// <summary>
/// Niedrigste Speicherabstraktion für lokalisierte Templates (ohne Fallback-Logik).
/// <para>
/// Aufgaben:
/// - Lesen/Schreiben konkreter Varianten (TenantScope × Culture × Key).
/// - Aufzählung für Export/Backoffice.
/// </para>
/// <para>
/// Hinweise:
/// - Fallback (Tenant-Chain/Kultur) gehört NICHT hierher; das machen höhere Services.
/// - Version/ETag-Handling erfolgt über <see cref="I18NTemplate.Version"/> (optimistisches Locking optional).
/// </para>
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Lädt eine konkrete Variante (ohne Fallback).
    /// Gibt null zurück, wenn für genau diesen Scope/Key/Culture kein Eintrag existiert.
    /// </summary>
    Task<I18NTemplate?> TryGetExactAsync(
        string tenantScope,
        I18NKey key,
        CultureInfo culture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt eine Variante an oder überschreibt sie.
    /// </summary>
    /// <param name="expectedVersion">
    /// Wenn gesetzt, wird geprüft, ob die existierende Version diesem Wert entspricht (Optimistic Concurrency).
    /// Wirft eine Exception (z.B. <see cref="System.IO.IOException"/> oder ConcurrencyException), wenn mismatch.
    /// </param>
    Task UpsertAsync(
        string tenantScope,
        I18NKey key,
        CultureInfo culture,
        string template,
        string? contentType = null,
        string? expectedVersion = null,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Löscht eine konkrete Variante.
    /// </summary>
    /// <returns>True, wenn gelöscht wurde; False, wenn nicht vorhanden.</returns>
    Task<bool> DeleteAsync(
        string tenantScope,
        I18NKey key,
        CultureInfo culture,
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