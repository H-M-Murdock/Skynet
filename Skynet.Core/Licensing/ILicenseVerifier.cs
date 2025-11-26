using Skynet.Core.Tenant;

namespace Skynet.Core.Licensing;

/// <summary>
/// Schnittstelle zur Überprüfung von Lizenzhüllen (Envelopes).
/// Implementierungen prüfen typischerweise kryptografische Integrität (Signatur) und zeitliche Gültigkeit.
/// </summary>
public interface ILicenseVerifier
{
    /// <summary>
    /// Verifiziert die übergebene <see cref="LicenseEnvelope"/>.
    /// <para>
    /// Ablauf der Prüfung:
    /// 1. Prüfung des Zeitfensters (NotBefore, Expires).
    /// 2. Prüfung der kryptografischen Signatur über die kanonisierte Hülle.
    /// </para>
    /// </summary>
    /// <param name="envelope">Die zu prüfende Lizenz.</param>
    /// <param name="defaultClaims">Optionale Standard-Claims, die der <see cref="LicenseInfo"/> hinzugefügt werden.</param>
    /// <param name="requireValidSignature">
    /// Wenn <c>true</c>, wird bei ungültiger Signatur oder abgelaufenem Zeitfenster eine <see cref="System.Security.Cryptography.CryptographicException"/> geworfen.
    /// Wenn <c>false</c>, wird ein <see cref="LicenseInfo"/>-Objekt zurückgegeben, in dem <see cref="LicenseInfo.SignatureValid"/> auf <c>false</c> steht.
    /// </param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>Das Ergebnis der Prüfung als domänisches Info-Objekt.</returns>
    /// <exception cref="ArgumentNullException">Wenn envelope null ist.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Wenn <paramref name="requireValidSignature"/> true ist und die Prüfung fehlschlägt.
    /// </exception>
    Task<LicenseInfo> VerifyAsync(
        LicenseEnvelope envelope,
        IReadOnlyDictionary<string, string>? defaultClaims = null,
        bool requireValidSignature = true,
        CancellationToken ct = default);
}