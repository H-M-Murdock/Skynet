namespace Skynet.Core.Licensing;

/// <summary>
/// Ergebnis des ersten Handshake-Schritts.
/// </summary>
/// <param name="Request">Das DTO, das an den Server gesendet wird.</param>
/// <param name="EphemeralPrivateKey">Der private Schlüssel, der bis zur Antwort sicher verwahrt werden muss.</param>
public sealed record ClientHandshakeStartResult(
    ClientLicenseRequest Request,
    byte[] EphemeralPrivateKey
);