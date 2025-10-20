using System.Text;

namespace Skynet.Core.Licensing;

/// <summary>
/// Kanonisiert eine LicenseEnvelope (ohne Signature-Feld) zu einem stabilen Byte-Array,
/// das server- und clientseitig identisch erzeugt werden muss, um Signaturen zu verifizieren.
/// HKDF/AAD-Konventionen:
/// - HKDF-Info: UTF8("tenant/{TenantId}/license-v1")
/// - HKDF-Salt: NonceClient || NonceServer
/// - AEAD-AAD (für DEK-Wrap/Secrets): UTF8("tenant/{TenantId};purpose=license;v=1")
/// </summary>
public interface ILicenseCanonicalizer
{
    /// <summary>
    /// Erzeugt einen deterministischen, kanonischen Byte-Stream der Envelope ohne Signature-Feld.
    /// Wirft ArgumentException bei ungültigen Feldern (z. B. fehlende Pflichtfelder).
    /// </summary>
    byte[] CanonicalizeWithoutSignature(LicenseEnvelope envelope, Encoding? encoding = null);
}
