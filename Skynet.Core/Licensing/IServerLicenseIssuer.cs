namespace Skynet.Core.Licensing;

/// <summary>
/// Stellt signierte Lizenzhüllen aus (Zero‑Knowledge: ohne DEK‑Klartext).
/// Implementierungen müssen Eingaben validieren (Algo/Keylängen) und bei Krypto‑Fehlern
/// CryptographicException werfen.
/// </summary>
public interface IServerLicenseIssuer
{
    /// <summary>
    /// Erzeugt eine signierte Lizenzhülle: vergibt TenantId (falls in serverMeta noch leer),
    /// generiert ephemeres Server‑ECDH (PubS), NonceS, setzt Algorithmenfelder und signiert.
    /// </summary>
    /// <exception cref="ArgumentException">bei unzulässigen Parametern/Formaten</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">bei Krypto-/Signaturfehlern</exception>
    Task<LicenseEnvelope> IssueEnvelopeAsync(
        byte[] clientPubEcdh,
        ClientInitMeta clientMeta,
        ServerInitMeta serverMeta,
        CancellationToken ct = default);

    /// <summary>
    /// Liefert den öffentlichen Signaturschlüssel und optional dessen KeyId (kid) für Client‑Verifikation.
    /// </summary>
    (byte[] PublicKey, string? KeyId) GetServerSignPublicKey();
}