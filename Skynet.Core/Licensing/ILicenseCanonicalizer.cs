using System.Text;

namespace Skynet.Core.Licensing;

/// <summary>
/// Kanonisiert eine LicenseEnvelope (ohne Signature-Feld) zu einem stabilen Byte-Array,
/// das server- und clientseitig identisch erzeugt werden muss, um Signaturen zu verifizieren.
/// </summary>
public interface ILicenseCanonicalizer
{
    /// <summary>
    /// Erzeugt einen deterministischen, kanonischen Byte-Stream der Envelope ohne Signature-Feld.
    /// Wirft ArgumentException bei ungültigen Feldern (z. B. fehlende Pflichtfelder).
    /// </summary>
    byte[] CanonicalizeWithoutSignature(LicenseEnvelope envelope, Encoding? encoding = null);
}
