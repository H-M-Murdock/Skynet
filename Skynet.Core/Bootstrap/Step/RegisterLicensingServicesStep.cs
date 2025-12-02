using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Skynet.Core.Crypto;
using Skynet.Core.Licensing;
using Skynet.Core.Time;

namespace Skynet.Core.Bootstrap;

/// <summary>
/// Registriert die Client-seitigen Lizenzierungsdienste (Verifikation, DEK-Management).
/// </summary>
public sealed class RegisterLicensingServicesStep : IBootStep
{
    public string Name => "Register Licensing Services";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // 1. Optionen registrieren (In einer echten App kämen diese aus appsettings.json/EnvVars)
        // Hier setzen wir einen Placeholder oder lesen ihn aus context.Items, falls vorhanden.
        var options = new LicensingOptions();
        
        if (context.Items.TryGetValue("Licensing:ServerKey", out var keyObj) && keyObj is string keyStr)
        {
            options.ServerVerifyKeyBase64 = keyStr;
        }
        
        context.Services.TryAddSingleton(options);

        // 2. Basis-Komponenten (Stateless)
        context.Services.TryAddSingleton<ILicenseCanonicalizer, JsonLicenseCanonicalizer>();

        // 3. Client DEK Manager (benötigt IAead aus CryptoServices)
        context.Services.TryAddSingleton<IClientDekManager, ClientDekManager>();

        // 4. Verifier registrieren (Factory-Pattern, um den Key zu injizieren)
        context.Services.TryAddSingleton<ILicenseVerifier>(sp =>
        {
            var cryptoSignature = sp.GetRequiredService<ISignature>();
            var clock = sp.GetRequiredService<IClock>();
            var canonicalizer = sp.GetRequiredService<ILicenseCanonicalizer>();
            var opts = sp.GetRequiredService<LicensingOptions>();

            byte[] verifyKey;
            try 
            {
                // Falls kein Key konfiguriert ist, nutzen wir ein leeres Array (Verifikation wird fehlschlagen, aber Service steht)
                // oder werfen eine Exception. Hier: Safe Default für Startup.
                verifyKey = string.IsNullOrEmpty(opts.ServerVerifyKeyBase64) 
                    ? Array.Empty<byte>() 
                    : Convert.FromBase64String(opts.ServerVerifyKeyBase64);
            }
            catch (FormatException)
            {
                context.Logger.LogWarning("Invalid Base64 string provided for Licensing:ServerVerifyKey.");
                verifyKey = Array.Empty<byte>();
            }

            return new LicenseVerifier(cryptoSignature, verifyKey, clock, canonicalizer);
        });

        return Task.FromResult("Licensing services registered (Verification Only)");
    }
}
