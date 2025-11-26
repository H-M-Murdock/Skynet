namespace Skynet.Core.Tenant;

using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Repräsentiert den Kontext eines aktiven Mandanten innerhalb der Anwendung.
/// Enthält Identifikationsdaten sowie Informationen für Ressourcenzugriffe (Fallback) und Lokalisierung.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Die ID des aktuell aktiven Mandanten (der "Leaf"-Tenant).
    /// </summary>
    TenantId CurrentTenantId { get; }

    /// <summary>
    /// Eine geordnete Liste von Tenant-IDs für die hierarchische Auflösung von Ressourcen (Fallback-Kette).
    /// <para>
    /// Konvention:
    /// 1. Eintrag [0]: Muss <see cref="CurrentTenantId"/> sein.
    /// 2. Eintrag [1..n]: Optionale Parent-Tenants (vom spezifischsten zum allgemeinsten).
    /// 3. Letzter Eintrag: Oft der System- oder Global-Tenant.
    /// </para>
    /// </summary>
    IReadOnlyList<TenantId> ResolutionChain { get; }

    /// <summary>
    /// Der optionale Anzeigename des Mandanten (z. B. "ACME Corp Europe").
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Eine optionale Beschreibung (z. B. für interne Notizen, Admin-UIs oder Logs).
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Die Standard-Kultur dieses Mandanten.
    /// Dient als Basis für Formatierungen, wenn keine benutzerspezifische Kultur vorliegt.
    /// </summary>
    CultureInfo DefaultCulture { get; }

    /// <summary>
    /// Die ID des direkten Eltern-Mandanten, falls eine Hierarchie existiert.
    /// </summary>
    TenantId? ParentTenantId { get; }
}