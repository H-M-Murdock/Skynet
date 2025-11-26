using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>
/// Steuert den kryptografischen Handshake zur Lizenzbeschaffung auf Client-Seite.
/// </summary>
public interface IClientLicenseInitiator
{
    /// <summary>
    /// Erzeugt den initialen Request-Payload für den Server.
    /// Generiert hierfür ein ephemeres ECDH-Schlüsselpaar und eine Nonce.
    /// </summary>
    /// <param name="meta">Metadaten über den Client (Name, ID, etc.).</param>
    /// <returns>
    /// Ein Resultat-Objekt, das sowohl den Request für den Server enthält, 
    /// als auch den privaten Schlüssel (State), der für die Finalisierung benötigt wird.
    /// </returns>
    Task<ClientHandshakeStartResult> CreateInitAsync(
        ClientInitMeta meta,
        CancellationToken ct = default);

    /// <summary>
    /// Verarbeitet die Antwort vom Server (LicenseEnvelope).
    /// <list type="number">
    /// <item>Verifiziert die Server-Signatur und das Zeitfenster.</item>
    /// <item>Führt ECDH (PrivC + PubS) durch.</item>
    /// <item>Leitet Session-Key K ab (HKDF).</item>
    /// <item>Generiert ggf. den DEK und verpackt ihn (dieser Schritt ist oft Teil des LicenseManagers, kann aber hier vorbereitet werden).</item>
    /// </list>
    /// </summary>
    /// <param name="serverEnvelope">Die empfangene, signierte Hülle.</param>
    /// <param name="clientPrivateEcdh">Der private Schlüssel aus Schritt 1.</param>
    /// <param name="clientProofSecret">Optional: Secret für gegenseitige Authentifizierung.</param>
    /// <returns>Die validierte LicenseEnvelope (ggf. angereichert/geprüft).</returns>
    Task<LicenseEnvelope> FinalizeAsync(
        LicenseEnvelope serverEnvelope,
        byte[] clientPrivateEcdh,
        byte[]? clientProofSecret = null,
        CancellationToken ct = default);

    /// <summary>
    /// Hilfsmethode: Leitet den Session-Key (K) ab.
    /// Normalerweise intern von <see cref="FinalizeAsync"/> genutzt, aber hier exposet für Diagnose oder manuelle Schritte.
    /// HKDF-Info: "tenant/{id}/license-v1".
    /// </summary>
    byte[] DeriveSessionKey(
        byte[] clientPrivEcdh,
        byte[] serverPubEcdh,
        byte[] nonceClient,
        byte[] nonceServer,
        TenantId tenantId);
        
    // Async-Derive entfernt, da reines HKDF CPU-bound ist (außer bei HSM, siehe Anmerkung IClientDekManager).
}