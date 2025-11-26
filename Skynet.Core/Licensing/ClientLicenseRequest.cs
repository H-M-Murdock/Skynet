namespace Skynet.Core.Licensing;

/// <summary>
/// DTO für die initiale Lizenzanfrage vom Client an den Server.
/// Enthält den öffentlichen Schlüssel des Clients (für den ECDH-Handshake),
/// eine Nonce und Metadaten über den Client.
/// </summary>
/// <param name="ClientPubEcdhSpkiBase64">
/// Der öffentliche Schlüssel des Clients im SPKI-Format (Subject Public Key Info), Base64-codiert.
/// </param>
/// <param name="NonceClientBase64">
/// Zufallszahl (Nonce) des Clients für den HKDF-Handshake, Base64-codiert.
/// </param>
/// <param name="Meta">
/// Metadaten über den Antragsteller (Name, Kontakt, etc.).
/// </param>
/// <param name="Kem">
/// Key Encapsulation Mechanism. Erwartet: "X25519" (empfohlen) oder "P256".
/// </param>
/// <param name="Kdf">
/// Key Derivation Function. Erwartet: "HKDF-SHA256".
/// </param>
/// <param name="Aead">
/// Authenticated Encryption Algorithm. Erwartet: "AES-256-GCM".
/// </param>
/// <param name="AppId">
/// Anwendungskennung, z. B. "skynet.core".
/// </param>
/// <param name="Version">
/// Version des Protokolls oder der Anwendung (z. B. "1.0").
/// </param>
public sealed record ClientLicenseRequest(
    string ClientPubEcdhSpkiBase64,
    string NonceClientBase64,
    ClientInitMeta Meta,
    string Kem = "X25519",        // Default auf empfohlene Werte
    string Kdf = "HKDF-SHA256",
    string Aead = "AES-256-GCM",
    string AppId = "skynet.core",
    string Version = "1.0"
);
