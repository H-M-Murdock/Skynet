using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Skynet.Core.Crypto;
using Skynet.Core.Licensing;

// LicenseRequest CLI:
//   init | i [--file client-meta.json]     → Meta-JSON (ClientId UUID, DeviceId aus OS-ID/Hostname gehasht)
//   genkey | g [--priv client-ecdh.priv] [--pub client-ecdh.pub] → ECDH-Keypair (X25519) erzeugen
//   info

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

                case "init":
                case "i":
                    await CmdInitAsync(rest);
                    return 0;

                case "genkey":
                case "g":
                    await CmdGenKeyAsync(rest);
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
        Console.WriteLine("      Erzeugt ein ECDH-Keypair (X25519) und speichert Base64-Dateien.");
        Console.WriteLine();
        Console.WriteLine("Beispiele:");
        Console.WriteLine("  dotnet run -- init --file client-meta.json");
        Console.WriteLine("  dotnet run -- genkey --priv client-ecdh.priv --pub client-ecdh.pub");
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

        // P-256 Keypair (PKCS#8 + SPKI), Base64 speichern
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var privPkcs8 = ecdh.ExportPkcs8PrivateKey();
        var pubSpki = ecdh.ExportSubjectPublicKeyInfo();

        await File.WriteAllTextAsync(privPath, Convert.ToBase64String(privPkcs8), Encoding.UTF8);
        await File.WriteAllTextAsync(pubPath, Convert.ToBase64String(pubSpki), Encoding.UTF8);

        Console.WriteLine("ECDH P-256 Schlüssel erzeugt.");
        Console.WriteLine($"Private Key (PKCS#8): {privPath}");
        Console.WriteLine($"Public  Key (SPKI):   {pubPath}");
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
        catch
        {
            // still fallback
        }

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
            // Windows: HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
            // .NET: Registry nur über Microsoft.Win32 verfügbar (Windows-only).
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid") as string;
            }
            catch
            {
                return null;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: /etc/machine-id
            try
            {
                const string path = "/etc/machine-id";
                if (File.Exists(path))
                {
                    var s = File.ReadAllText(path).Trim();
                    return string.IsNullOrWhiteSpace(s) ? null : s;
                }
            }
            catch
            {
                return null;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: IOPlatformUUID via ioreg (einfacher Ansatz ohne zusätzliche Libs)
            try
            {
                // ioreg -rd1 -c IOPlatformExpertDevice | awk -F\" '/IOPlatformUUID/{print $4}'
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
            catch
            {
                return null;
            }
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
}