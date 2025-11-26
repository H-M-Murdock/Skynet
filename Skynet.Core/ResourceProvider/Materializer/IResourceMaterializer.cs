// Skynet.Core.Materialization/IResourceMaterializer.cs

using Skynet.Core.Tenant;

namespace Skynet.Core.ResourceProvider;

/// <summary>
/// High-Level API für den Zugriff auf typisierte Ressourcen.
/// Kapselt die Orchestrierung von Locator, Deserializern und Convertern.
/// </summary>
public interface IResourceMaterializer
{
    // --- READ (Tenant implizit via ITenantContextAccessor.Current) ---

    /// <summary>
    /// Versucht, eine Ressource abzurufen und in Typ T zu wandeln.
    /// Gibt (true, value) bei Erfolg zurück, oder (false, default) bei NotFound/Fehler.
    /// </summary>
    Task<(bool success, T? value)> TryGetAsync<T>(
        string logicalKey, 
        CancellationToken ct = default);

    /// <summary>
    /// Ruft eine Ressource ab. Wirft Exceptions bei NotFound oder Konvertierungsfehlern.
    /// </summary>
    Task<T?> GetAsync<T>(
        string logicalKey, 
        CancellationToken ct = default);


    // --- READ (expliziter Tenant) ---

    Task<(bool success, T? value)> TryGetAsync<T>(
        ITenantContext tenant, 
        string logicalKey, 
        CancellationToken ct = default);

    Task<T?> GetAsync<T>(
        ITenantContext tenant, 
        string logicalKey, 
        CancellationToken ct = default);


    // --- WRITE (Tenant implizit) ---

    Task WriteAsync<T>(
        string logicalKey, 
        T value, 
        string? ifMatch = null, 
        CancellationToken ct = default);


    // --- WRITE (expliziter Tenant) ---

    Task WriteAsync<T>(
        ITenantContext tenant, 
        string logicalKey, 
        T value, 
        string? ifMatch = null, 
        CancellationToken ct = default);


    // --- DELETE ---

    Task DeleteAsync(
        string logicalKey, 
        string? ifMatch = null, 
        CancellationToken ct = default);

    Task DeleteAsync(
        ITenantContext tenant, 
        string logicalKey, 
        string? ifMatch = null, 
        CancellationToken ct = default);
}
