// Skynet.Core.Bootstrap/Steps/RegisterLocalAesProtectorStep.cs

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Crypto;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert einen LocalAesProtector:
/// - Schlüssel aus Config: Crypto:LocalAes:KeyBase64 (Base64 von 16/24/32 Bytes)
/// - Fallback: stabiler Default-Key im Code (nur für Bootstrap/Core-Fallback!)
/// </summary>
public sealed class RegisterLocalAesProtectorStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Core;

    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // Config holen (vom BootstrapConfigStep bereitgestellt)
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetService<IConfiguration>();

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
                    (key, source) = GetDefaultKey();
                }
            }
            catch
            {
                (key, source) = GetDefaultKey();
            }
        }
        else
        {
            (key, source) = GetDefaultKey();
        }

        services.AddSingleton<ISecretProtector>(new InitAesProtector(key));
        _report = $"local AES protector registered (keySource={source}, keySize={key.Length * 8}bit)";
        return Task.CompletedTask;
    }

    public string GetReport() => _report;

    private static (byte[] key, string source) GetDefaultKey()
    {
        // Stabiler Default (nur für Bootstrap/Core-Fallback; später durch Locator/Secrets ersetzen!)
        // 32 Bytes (AES-256). In echter Produktion rotation & sichere Quelle.
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
