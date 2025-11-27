using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Crypto;

namespace Skynet.Core.Bootstrap;

public class RegisterCryptoServicesStep : IBootStep
{
    public string Name => "Register Crypto Services";

    public Task<string> ExecuteAsync(BootstrapContext context)
    {
        // 1. Symmetrische Verschlüsselung (AES-GCM)
        // Zustandslos, Thread-Safe -> Singleton
        context.Services.AddSingleton<IAead, AesGcmAead>();

        // 2. Key Derivation (HKDF)
        // Zustandslos -> Singleton
        context.Services.AddSingleton<IKeyDerivation, KeyDerivation>();

        // 3. Key Agreement (ECDH P-256)
        // Die Implementierung ist konfigurierbar (Algorithmus), aber hier fest P-256.
        // Da EcdhKeyAgreement im Konstruktor den Algorithmus setzt und sonst zustandslos ist: Singleton.
        context.Services.AddSingleton<IEcdhKeyAgreement>(new EcdhKeyAgreement());

        // 4. Key Pair Generator
        // Nutzt intern IKeyDerivation (wenn man Seed-basiert generiert).
        // Wir registrieren ihn so, dass er Abhängigkeiten auflösen kann, falls er sie via DI bräuchte.
        // Aktuell nutzt deine Implementierung 'new KeyDerivation()' intern statisch/direkt, 
        // aber für Sauberkeit registrieren wir ihn als Service.
        context.Services.AddSingleton<IKeyPairGenerator>(sp => new DefaultKeyPairGenerator());

        // Optional: SecretProtector (High-Level API)
        // Er braucht einen Master-Key. Da wir hier noch keine Config haben, 
        // registrieren wir ihn meist erst später (wenn Config geladen ist) oder als Factory.
        // Für den Moment lassen wir ihn hier raus, da er Config-abhängig ist (KeyBase64).

        return Task.FromResult("Registered: IAead, IKeyDerivation, IEcdhKeyAgreement, IKeyPairGenerator");
    }
}
