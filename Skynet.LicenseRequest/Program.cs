using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Skynet.Core.Licensing;

// LicenseRequest CLI:
//   init | i [--file client-meta.json]
//   genkey | g [--priv client-ecdh.priv] [--pub client-ecdh.pub]
//   request | r [--meta client-meta.json] [--pub client-ecdh.pub] [--out license-request.json] (-p1..-p9) [--ver 1.0.0]
//   info

namespace Skynet.LicenseRequest;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    // Feste Produkt-GUIDs (DummyApp01..DummyApp09)
    private static readonly (string Switch, string Name, string Guid)[] ProductCatalog =
    [
        ("-p1", "DummyApp01", "8a9b7b9f-9f3b-4e9b-9d9f-1c2a3b4c5d6e"),
        ("-p2", "DummyApp02", "d1f2e3c4-b5a6-4789-9abc-def012345678"),
        ("-p3", "DummyApp03", "3f1e2d4c-5b6a-4789-8abc-01def2345678"),
        ("-p4", "DummyApp04", "a2b3c4d5-e6f7-4a89-9b0c-1234567890ab"),
        ("-p5", "DummyApp05", "0c1b2a3d-4e5f-4678-9abc-d0123456789f"),
        ("-p6", "DummyApp06", "9e8d7c6b-5a4f-4e3d-8c2b-1a0f9e8d7c6b"),
        ("-p7", "DummyApp07", "f0e1d2c3-b4a5-4c6d-8e9f-0123456789ab"),
        ("-p8", "DummyApp08", "12ab34cd-56ef-4789-9a0b-cdef01234567"),
        ("-p9", "DummyApp09", "98ba76dc-54fe-4321-9abc-def001234567"),
    ];

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

                case "init":
                case "i":
                    await CmdInitAsync(rest);
                    return 0;

                case "genkey":
                case "g":
                    await CmdGenKeyAsync(rest);
                    return 0;

                case "request":
                case "r":
                    await CmdRequestAsync(rest);
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

    private static bool IsHelp(string s)
        => s is "-h" or "--help" or "help" or "/?" or "/help";

    private static void PrintHelp()
    {
        Console.WriteLine("LicenseRequest CLI");
        Console.WriteLine();
        Console.WriteLine("Befehle:");
        Console.WriteLine("  info");
        Console.WriteLine("      Zeigt Tool-/Runtime-Informationen.");
        Console.WriteLine("  init | i [--file client-meta.json]");
        Console.WriteLine("      Erstellt eine Meta-JSON mit zufälliger ClientId und DeviceId (OS-ID/Hostname gehasht).");
        Console.WriteLine("  genkey | g [--priv client-ecdh.priv] [--pub client-ecdh.pub]");
        Console.WriteLine("      Erzeugt ein ECDH-Keypair (P-256) und speichert Base64-Dateien (PKCS#8/SPKI).");
        Console.WriteLine("  request | r [--meta client-meta.json] [--pub client-ecdh.pub] [--out license-request.json] (-p1..-p9) [--ver 1.0.0]");
        Console.WriteLine("      Erstellt die Request-Datei (ClientLicenseRequest) für den Server.");
        Console.WriteLine();
        Console.WriteLine("Produkte (Schalter → AppId GUID):");
        foreach (var p in ProductCatalog)
            Console.WriteLine($"  {p.Switch.PadRight(4)} {p.Name.PadRight(14)} {p.Guid}");
        Console.WriteLine();
        Console.WriteLine("Beispiele:");
        Console.WriteLine("  dotnet run -- init --file client-meta.json");
        Console.WriteLine("  dotnet run -- genkey --priv client-ecdh.priv --pub client-ecdh.pub");
        Console.WriteLine("  dotnet run -- request --meta client-meta.json --pub client-ecdh.pub --out license-request.json -p1 --ver 1.0.0");
        Console.WriteLine("  dotnet run -- info");
    }

    private static void CmdInfo()
    {
        Console.WriteLine("LicenseRequest Client-Utilities");
        Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS:      {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Console.WriteLine($"CPU:     {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"WD:      {Environment.CurrentDirectory}");
        Console.WriteLine($"Host:    {Environment.MachineName}");
    }

    private static async Task CmdInitAsync(string[] args)
    {
        var file = GetArgValue(args, "--file") ?? "client-meta.json";

        var clientId = Guid.NewGuid().ToString("D");
        var (deviceId, source) = ComputeDeviceId();

        var meta = new ClientInitMeta(
            name: "CHANGE-ME",
            description: null,
            address: null,
            contactName: null,
            contactEmail: null,
            contactPhone: null,
            clientId: clientId,
            deviceId: deviceId,
            tags: new Dictionary<string, string> { { "exampleFlag", "true" } }
        );

        var json = JsonSerializer.Serialize(meta, JsonOpts);
        await File.WriteAllTextAsync(file, json, Encoding.UTF8);
        Console.WriteLine($"Meta-Datei geschrieben: {file}");
        Console.WriteLine($"ClientId: {clientId}");
        Console.WriteLine($"DeviceId: {deviceId} (Quelle: {source})");
        Console.WriteLine("Bitte 'name' anpassen. Optionale Felder nach Bedarf ausfüllen.");
    }

    private static async Task CmdGenKeyAsync(string[] args)
    {
        var privPath = GetArgValue(args, "--priv") ?? "client-ecdh.priv";
        var pubPath = GetArgValue(args, "--pub") ?? "client-ecdh.pub";

        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var privPkcs8 = ecdh.ExportPkcs8PrivateKey();
        var pubSpki = ecdh.ExportSubjectPublicKeyInfo();

        await File.WriteAllTextAsync(privPath, Convert.ToBase64String(privPkcs8), Encoding.UTF8);
        await File.WriteAllTextAsync(pubPath, Convert.ToBase64String(pubSpki), Encoding.UTF8);

        Console.WriteLine("ECDH P-256 Schlüssel erzeugt.");
        Console.WriteLine($"Private Key (PKCS#8): {privPath}");
        Console.WriteLine($"Public  Key (SPKI):   {pubPath}");
    }

    private static async Task CmdRequestAsync(string[] args)
    {
        var metaPath = GetArgValue(args, "--meta") ?? "client-meta.json";
        var pubPath = GetArgValue(args, "--pub") ?? "client-ecdh.pub";
        var outPath = GetArgValue(args, "--out") ?? "license-request.json";
        var version = GetArgValue(args, "--ver") ?? GetAssemblyVersion();

        if (!File.Exists(metaPath)) throw new FileNotFoundException("Meta-Datei nicht gefunden.", metaPath);
        if (!File.Exists(pubPath)) throw new FileNotFoundException("Public-Key-Datei nicht gefunden.", pubPath);

        var (appIdGuid, appName, switchUsed) = ResolveProduct(args);
        if (appIdGuid is null)
            throw new ArgumentException("Bitte Produkt angeben (-p1..-p9). Siehe Hilfe.");

        var metaJson = await File.ReadAllTextAsync(metaPath, Encoding.UTF8);
        var meta = JsonSerializer.Deserialize<ClientInitMeta>(metaJson, JsonOpts)
                   ?? throw new InvalidOperationException("Meta-Datei ist leer/ungültig.");
        var (ok, metaError) = ValidateMeta(meta);
        if (!ok) throw new ArgumentException($"Meta ungültig: {metaError}");

        var pubB64 = (await File.ReadAllTextAsync(pubPath, Encoding.UTF8)).Trim();
        _ = Convert.FromBase64String(pubB64);

        var nonceClientB64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));

        var dto = new ClientLicenseRequest(
            ClientPubEcdhSpkiBase64: pubB64,
            NonceClientBase64: nonceClientB64,
            Meta: meta,
            Kem: "ECDH-P256",
            Kdf: "HKDF-SHA256",
            Aead: "AES-256-GCM",
            AppId: appIdGuid,
            Version: version
        );

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        await File.WriteAllTextAsync(outPath, json, Encoding.UTF8);
        Console.WriteLine($"Request-Datei geschrieben: {outPath}");
        Console.WriteLine($"Produkt: {appName} ({switchUsed}), AppId={appIdGuid}, Version={version}");
    }

    private static (string? appIdGuid, string appName, string switchUsed) ResolveProduct(string[] args)
    {
        foreach (var p in ProductCatalog)
        {
            if (args.Any(a => string.Equals(a, p.Switch, StringComparison.OrdinalIgnoreCase)))
                return (p.Guid, p.Name, p.Switch);
        }
        return (null, "<unbekannt>", "<none>");
    }

    // DeviceId: OS-Machine-ID → SHA-256 hex; Fallback Hostname → SHA-256 hex
    private static (string deviceId, string source) ComputeDeviceId()
    {
        try
        {
            var osId = TryGetOsMachineId();
            if (!string.IsNullOrEmpty(osId))
                return (Sha256Hex(osId), "OS-Machine-ID");
        }
        catch { /* fallback */ }

        var host = Environment.MachineName ?? "unknown-host";
        return (Sha256Hex(host), "Hostname");
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? TryGetOsMachineId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid") as string;
            }
            catch { return null; }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                const string path = "/etc/machine-id";
                if (File.Exists(path))
                {
                    var s = File.ReadAllText(path).Trim();
                    return string.IsNullOrWhiteSpace(s) ? null : s;
                }
            }
            catch { return null; }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/sbin/ioreg",
                    ArgumentList = { "-rd1", "-c", "IOPlatformExpertDevice" },
                    RedirectStandardOutput = true
                };
                using var p = System.Diagnostics.Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                var marker = "IOPlatformUUID\" = \"";
                var idx = output.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    idx += marker.Length;
                    var end = output.IndexOf('\"', idx);
                    if (end > idx)
                        return output.Substring(idx, end - idx);
                }
            }
            catch { return null; }
        }

        return null;
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

    private static string GetAssemblyVersion()
    {
        var asm = typeof(Program).Assembly.GetName().Version;
        return asm is null ? "1.0.0" : $"{asm.Major}.{asm.Minor}.{asm.Build}";
    }
    
    // Rudimentäre Metadaten-Prüfung gegen offensichtliche Probleme:
    // - Trimmen und Längenbegrenzung (max 256 pro Feld, Tags-Werte 256, Keys 64)
    // - Verbot einfacher HTML-Tags/Script-Fragmente
    // - Verbot gängiger SQL-Injection-Payload-Fragmente
    // - Keine überlangen Whitespaces-Sequenzen (>3) – wird als Fehler gewertet
    private static (bool Ok, string Error) ValidateMeta(ClientInitMeta meta)
    {
        (bool Ok, string Err) Fail(string e) => (false, e);

        (bool Ok, string Err) CheckString(string? s, string field, int maxLen = 256)
        {
            if (string.IsNullOrWhiteSpace(s)) return (true, "");
            var trimmed = s.Trim();
            if (trimmed.Length == 0) return Fail($"{field}: nur Leerzeichen.");
            if (trimmed.Length > maxLen) return Fail($"{field}: zu lang (>{maxLen}).");
            if (trimmed.Contains("    ") || trimmed.Contains("\t\t\t")) return Fail($"{field}: enthält ungewöhnlich viele Whitespaces.");

            var lower = trimmed.ToLowerInvariant();
            string[] badHtml = { "<script", "</script", "<iframe", "</iframe", "<img", "<a ", "<div", "</div", "<style", "</style" };
            if (badHtml.Any(b => lower.Contains(b))) return Fail($"{field}: enthält HTML/Script-Tags.");

            string[] badSql = { " or ", " and ", " union ", " select ", " insert ", " update ", " delete ", " drop ", " --", ";--", "' or '1'='1", "\" or \"1\"=\"1" };
            if (badSql.Any(b => lower.Contains(b))) return Fail($"{field}: enthält mögliche SQL-Injection-Fragmente.");

            string[] badTokens = { "<?", "?>", "<!", "</", "-->", "<%", "%>", "${", "{{", "}}" };
            if (badTokens.Any(t => trimmed.Contains(t, StringComparison.Ordinal))) return Fail($"{field}: enthält verbotene Markup/Template-Tokens.");

            return (true, "");
        }

        if (string.IsNullOrWhiteSpace(meta.Name) || meta.Name.Trim().Length == 0) return Fail("name: Pflichtfeld fehlt/leer.");
        var res = CheckString(meta.Name, "name", 128); if (!res.Ok) return res;

        res = CheckString(meta.Description, "description"); if (!res.Ok) return res;
        res = CheckString(meta.Address, "address"); if (!res.Ok) return res;
        res = CheckString(meta.ContactName, "contactName", 128); if (!res.Ok) return res;
        res = CheckString(meta.ContactEmail, "contactEmail", 128); if (!res.Ok) return res;
        res = CheckString(meta.ContactPhone, "contactPhone", 64); if (!res.Ok) return res;
        res = CheckString(meta.ClientId, "clientId", 128); if (!res.Ok) return res;
        res = CheckString(meta.DeviceId, "deviceId", 128); if (!res.Ok) return res;

        if (meta.Tags is not null)
        {
            if (meta.Tags.Count > 64) return Fail("tags: zu viele Einträge (>64).");
            foreach (var kv in meta.Tags)
            {
                var key = kv.Key?.Trim();
                var val = kv.Value?.Trim();
                if (string.IsNullOrWhiteSpace(key) || key!.Length > 64) return Fail("tags: ungültiger Key (leer/zu lang).");
                res = CheckString(val, $"tags[{key}]", 256); if (!res.Ok) return res;
            }
        }

        return (true, "");
    }
    
}