namespace Skynet.Core.Crypto;


/// <summary>
/// Digitale Signaturen für Lizenzhüllen. Implementierungen müssen
/// Eingaben (Key-/Datenlängen) validieren und bei Fehlern ArgumentException werfen.
/// Verify gibt true/false zurück, wirft nicht bei ungültiger Signatur.
/// </summary>
public interface ISignature
{
    SignatureAlgorithm Algorithm { get; }

    /// <summary>
    /// Signiert Daten (roh oder vorgehashte Daten je nach Implementierung).
    /// Wirft ArgumentException bei ungültigen Key-/Datenlängen.
    /// </summary>
    byte[] Sign(byte[] privateKey, byte[] data);

    /// <summary>
    /// Verifiziert eine detached Signatur. Gibt true bei gültig, sonst false.
    /// Wirft ArgumentException bei ungültigen Eingaben (Key-/Signaturlängen).
    /// </summary>
    bool Verify(byte[] publicKey, byte[] data, byte[] signature);

    /// <summary>
    /// Optional: Kennung des verwendeten Keys (KeyId/kid) für Rotation/Zuordnung.
    /// </summary>
    string? KeyId { get; }
}