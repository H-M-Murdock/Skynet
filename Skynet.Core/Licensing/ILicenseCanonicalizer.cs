namespace Skynet.Core.Licensing;

/// <summary>
/// Kanonisiert eine LicenseEnvelope (ohne Signature-Feld) zu einem stabilen Byte-Array,
/// das server- und clientseitig identisch erzeugt werden muss, um Signaturen zu verifizieren.
/// <para>
/// HKDF/AAD-Konventionen (Ref: LicenseGlossary):
/// <list type="bullet">
/// <item>HKDF-Info: UTF8("tenant/{TenantId}/license-v1")</item>
/// <item>HKDF-Salt: NonceClient || NonceServer</item>
/// <item>AEAD-AAD (für DEK-Wrap/Secrets): UTF8("tenant/{TenantId};purpose=license;v=1")</item>
/// </list>
/// </para>
/// </summary>
public interface ILicenseCanonicalizer
{
    /// <summary>
    /// Erzeugt einen deterministischen, kanonischen Byte-Stream der Envelope ohne Signature-Feld.
    /// Verwendet strikt UTF-8 Encoding.
    /// <para>
    /// Die Reihenfolge der Felder und Formatierung (z. B. Datum als ISO-8601 "O") muss exakt eingehalten werden.
    /// </para>
    /// </summary>
    /// <param name="envelope">Die zu serialisierende Hülle.</param>
    /// <returns>Das Byte-Array, über das die Signatur berechnet wird.</returns>
    /// <exception cref="ArgumentNullException">Wenn envelope null ist.</exception>
    byte[] CanonicalizeWithoutSignature(LicenseEnvelope envelope);
}