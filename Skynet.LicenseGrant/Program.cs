using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Skynet.Core.Licensing;
using Skynet.Core.Tenant;

// Skynet.LicenseGrant CLI (Server-seitig):
// - info
// - genkey | g [--priv server-sign.priv] [--pub server-sign.pub]     → ECDSA P-256 (PKCS#8/SPKI) für Signatur
// - create | c --req license-request.json [--out license-envelope.json] [--kid KEY-ID] [--days 365]
//   Liest ClientLicenseRequest, erzeugt LicenseEnvelope (Gültigkeit mind. 365 Tage), signiert mit server-sign.priv

namespace Skynet.LicenseGrant;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var cmd = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        try
        {
            switch (cmd)
            {
                case "info":
                    CmdInfo();
                    return 0;

                case "genkey":
                case "g":
                    await CmdGenKeyAsync(rest);
                    return 0;

                case "create":
                case "c":
                    await CmdCreateAsync(rest);
                    return 0;

                default:
                    Console.WriteLine($"Unbekannter Befehl: {cmd}");
                    PrintHelp();
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string s) => s is "-h" or "--help" or "help" or "/?" or "/help";

    private static void PrintHelp()
    {
        Console.WriteLine("Skynet.LicenseGrant CLI");
        Console.WriteLine();
        Console.WriteLine("Befehle:");
        Console.WriteLine("  info");
        Console.WriteLine("      Zeigt Tool-/Runtime-Informationen.");
        Console.WriteLine("  genkey | g [--priv server-sign.priv] [--pub server-sign.pub]");
        Console.WriteLine("      Erzeugt Server-Signaturschlüssel (ECDSA P-256), PKCS#8/SPKI in Base64.");
        Console.WriteLine("  create | c --req license-request.json [--out license-envelope.json] [--kid KEY-ID] [--days 365]");
        Console.WriteLine("      Liest Client-Request und stellt eine LicenseEnvelope aus (min. 365 Tage gültig).");
        Console.WriteLine();
        Console.WriteLine("Beispiele:");
        Console.WriteLine("  dotnet run -- g --priv server-sign.priv --pub server-sign.pub");
        Console.WriteLine("  dotnet run -- c --req license-request.json --out license-envelope.json --kid key-2025 --days 730");
    }

    private static void CmdInfo()
    {
        Console.WriteLine("LicenseGrant Server-Utilities");
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS:      {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine($"CPU:     {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"WD:      {Environment.CurrentDirectory}");
    }

    private static async Task CmdGenKeyAsync(string[] args)
    {
        var privPath = GetArgValue(args, "--priv") ?? "server-sign.priv";
        var pubPath = GetArgValue(args, "--pub") ?? "server-sign.pub";

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privPkcs8 = ecdsa.ExportPkcs8PrivateKey();
        var pubSpki = ecdsa.ExportSubjectPublicKeyInfo();

        await File.WriteAllTextAsync(privPath, Convert.ToBase64String(privPkcs8), Encoding.UTF8);
        await File.WriteAllTextAsync(pubPath, Convert.ToBase64String(pubSpki), Encoding.UTF8);

        Console.WriteLine("Signaturschlüssel (ECDSA P-256) erzeugt.");
        Console.WriteLine($"Private Key (PKCS#8): {privPath}");
        Console.WriteLine($"Public  Key (SPKI):   {pubPath}");
    }

    private static async Task CmdCreateAsync(string[] args)
    {
        var reqPath = GetArgValue(args, "--req") ?? throw new ArgumentException("--req fehlt (ClientRequest).");
        var outPath = GetArgValue(args, "--out") ?? "license-envelope.json";
        var kid = GetArgValue(args, "--kid"); // optional
        var daysStr = GetArgValue(args, "--days");
        var days = Math.Max(365, TryParseInt(daysStr, 365)); // min. 365 Tage
        var privSignPath = "server-sign.priv";

        if (!File.Exists(reqPath)) throw new FileNotFoundException("Request-Datei nicht gefunden.", reqPath);
        if (!File.Exists(privSignPath)) throw new FileNotFoundException("Server-PrivateKey nicht gefunden (server-sign.priv).", privSignPath);

        // Request laden
        var reqJson = await File.ReadAllTextAsync(reqPath, Encoding.UTF8);
        var req = JsonSerializer.Deserialize<ClientLicenseRequest>(reqJson, JsonOpts)
                  ?? throw new InvalidOperationException("Request ist leer/ungültig.");

        // Nonce/Key prüfen
        var clientPubSpki = Convert.FromBase64String(req.ClientPubEcdhSpkiBase64);
        var nonceClient = Convert.FromBase64String(req.NonceClientBase64);
        if (nonceClient.Length != 12) throw new ArgumentException("NonceClient muss 12 Bytes (Base64) sein.");

        // Ephemeren Server-ECDH-Schlüssel generieren (P-256) und Public (SPKI) zur Envelope packen
        using var ecdhServer = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var serverPubSpki = ecdhServer.ExportSubjectPublicKeyInfo();

        // NonceServer (12B)
        var nonceServer = RandomNumberGenerator.GetBytes(12);

        // Zeiten und Tenant
        var now = DateTimeOffset.UtcNow;
        var issued = now;
        var notBefore = (DateTimeOffset?)null;
        var expires = now.AddDays(days);
        var tenantId = new TenantId(Guid.NewGuid());

        // Envelope-Algofelder abbilden (P-256/HKDF-SHA256/AES-256-GCM)
        var kem = KemAlgorithm.X25519; // Platzhalter Enum-Wert im Modell; P-256 derzeit nicht im Enum sichtbar
        var kdf = KdfAlgorithm.HkdfSha256;
        var aead = AeadAlgorithm.Aes256Gcm;

        // Envelope konstruieren (FeatureFlags null)
        var envelopeUnsigned = new LicenseEnvelope(
            tenantId: tenantId,
            serverPubEcdh: serverPubSpki,
            nonceClient: nonceClient,
            nonceServer: nonceServer,
            featureFlags: null,
            issuedAtUtc: issued,
            notBeforeUtc: notBefore,
            expiresUtc: expires,
            kemAlg: kem,
            kdfAlg: kdf,
            aeadAlg: aead,
            signKeyId: kid,
            signature: [] // wird gleich ersetzt
        );

        // Kanonisieren und signieren
        var canonicalizer = new JsonLicenseCanonicalizer();
        var data = canonicalizer.CanonicalizeWithoutSignature(envelopeUnsigned);

        var privSignB64 = await File.ReadAllTextAsync(privSignPath, Encoding.UTF8);
        var privSign = Convert.FromBase64String(privSignB64);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privSign, out _);
        var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);

        // Finale Envelope mit Signatur ausgeben
        var envelope = new LicenseEnvelope(
            tenantId: tenantId,
            serverPubEcdh: serverPubSpki,
            nonceClient: nonceClient,
            nonceServer: nonceServer,
            featureFlags: null,
            issuedAtUtc: issued,
            notBeforeUtc: notBefore,
            expiresUtc: expires,
            kemAlg: kem,
            kdfAlg: kdf,
            aeadAlg: aead,
            signKeyId: kid,
            signature: signature
        );

        // Serialisierung: einfache JSON-Ausgabe der Felder
        var outObj = new
        {
            tenantId = envelope.TenantId.ToString(),
            serverPubEcdh = Convert.ToBase64String(envelope.ServerPubEcdh),
            nonceClient = Convert.ToBase64String(envelope.NonceClient),
            nonceServer = Convert.ToBase64String(envelope.NonceServer),
            featureFlags = envelope.FeatureFlags, // derzeit null
            issuedAtUtc = envelope.IssuedAtUtc.UtcDateTime.ToString("O"),
            notBeforeUtc = envelope.NotBeforeUtc?.UtcDateTime.ToString("O"),
            expiresUtc = envelope.ExpiresUtc.UtcDateTime.ToString("O"),
            kemAlg = envelope.KemAlg.ToString(),
            kdfAlg = envelope.KdfAlg.ToString(),
            aeadAlg = envelope.AeadAlg.ToString(),
            signKeyId = envelope.SignKeyId,
            signature = Convert.ToBase64String(envelope.Signature)
        };

        var outJson = JsonSerializer.Serialize(outObj, JsonOpts);
        await File.WriteAllTextAsync(outPath, outJson, Encoding.UTF8);
        Console.WriteLine($"LicenseEnvelope geschrieben: {outPath}");
        Console.WriteLine($"TenantId: {tenantId}");
        Console.WriteLine($"Gültigkeit: {issued:O} bis {expires:O} ({days} Tage)");
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return i + 1 < args.Length ? args[i + 1] : null;
        }
        return null;
    }

    private static int TryParseInt(string? s, int fallback)
        => int.TryParse(s, out var v) ? v : fallback;
}