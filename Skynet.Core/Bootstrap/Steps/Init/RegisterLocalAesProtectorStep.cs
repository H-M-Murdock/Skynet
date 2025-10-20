// Skynet.Core.Bootstrap/Steps/RegisterLocalAesProtectorStep.cs

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skynet.Core.Crypto;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert einen SecretProtector:
/// - Schlüssel aus Config: Crypto:LocalAes:KeyBase64 (Base64 von 16/24/32 Bytes)
/// - Fallback: stabiler Default-Key im Code (nur für Bootstrap/Core-Fallback!)
/// Policy:
/// - In Produktionsumgebungen ist der Default-Key NICHT erlaubt (Abbruch mit klarer Fehlermeldung).
/// - Produktionsflag wird über ENV "ASPNETCORE_ENVIRONMENT=Production" oder Config-Schlüssel "Environment:Name=Production" erkannt.
/// </summary>
public sealed class RegisterLocalAesProtectorStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Init;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // AesGcmAead bereitstellen, falls nicht schon durch RegisterCryptoCoreServicesStep geschehen
        services.AddSingleton<IAead, AesGcmAead>();

        // Config holen (vom BootstrapConfigStep bereitgestellt)
        using var sp = services.BuildServiceProvider();
        var cfg = sp.GetService<IConfiguration>();
        var logger = sp.GetService<ILogger<RegisterLocalAesProtectorStep>>();

        // Environment-Name bestimmen (Defaults: Development)
        var env = cfg?["Environment:Name"]
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? "Development";
        var isProduction = string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);

        byte[] key;
        string source;

        var keyB64 = cfg?["Crypto:LocalAes:KeyBase64"];
        if (!string.IsNullOrWhiteSpace(keyB64))
        {
            try
            {
                var material = Convert.FromBase64String(keyB64);
                if (material.Length is 16 or 24 or 32)
                {
                    key = material;
                    source = "config";
                }
                else
                {
                    throw new InvalidOperationException("Crypto:LocalAes:KeyBase64 must decode to 16/24/32 bytes.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Invalid Base64 in Crypto:LocalAes:KeyBase64.", ex);
            }
        }
        else
        {
            // Kein Key konfiguriert → Default nur außerhalb Produktion zulassen
            (key, source) = GetDefaultKey();

            if (isProduction)
            {
                // Harte Policy: in Produktion abbrechen
                var msg =
                    "No Crypto:LocalAes:KeyBase64 configured while running in Production. " +
                    "Using the built-in default key is forbidden for security reasons. " +
                    "Please configure a 16/24/32-byte key (Base64) at 'Crypto:LocalAes:KeyBase64'.";
                logger?.LogCritical("{Message}", msg);
                throw new InvalidOperationException(msg);
            }

            // In Nicht-Prod: warnen
            logger?.LogWarning(
                "Using built-in DEFAULT AES key for SecretProtector in environment '{Env}'. Configure 'Crypto:LocalAes:KeyBase64' ASAP.",
                env);
        }

        // SecretProtector mit IAead registrieren
        services.AddSingleton<ISecretProtector>(sp2 =>
        {
            var aead = sp2.GetRequiredService<IAead>();
            return new SecretProtector(aead, key);
        });

        _report = $"secret protector registered (keySource={source}, keySize={key.Length * 8}bit, env={env})";
        return Task.CompletedTask;
    }

    public string GetReport() => _report;

    private static (byte[] key, string source) GetDefaultKey()
    {
        // Stabiler Default (nur für Bootstrap/Core-Fallback in Nicht-Prod).
        // 32 Bytes (AES-256).
        var key = new byte[]
        {
            0x5A,0x12,0x7C,0x3F,0x09,0xAB,0xCD,0xEE,
            0x10,0x20,0x30,0x40,0x50,0x60,0x70,0x80,
            0x90,0xA0,0xB0,0xC0,0xD0,0xE0,0xF0,0x0F,
            0x1A,0x2B,0x3C,0x4D,0x5E,0x6F,0x7A,0x8B
        };
        return (key, "default");
    }
}
