using Microsoft.Extensions.DependencyInjection;
using Skynet.Core.Crypto;

namespace Skynet.Core.Bootstrap;

public sealed class BootstrapCryptoServicesStep : IBootStep, IStepReport
{
    public RuntimeLevel MinLevel => RuntimeLevel.Bootstrap;
    public RuntimeLevel TargetLevel => RuntimeLevel.Init;

    private string _report = string.Empty;

    public Task ExecuteAsync(IServiceCollection services, CancellationToken ct)
    {
        // Symmetrisch (AEAD) + HKDF
        services.AddSingleton<IAead, AesGcmAead>();
        services.AddSingleton<IKeyDerivation, KeyDerivation>();

        // ECDH (X25519), Key-Paare, Signaturen (ECDSA P-256)
        services.AddSingleton(new EcdhKeyAgreement());
        services.AddSingleton(new DefaultKeyPairGenerator());
        services.AddSingleton(new SignatureP256());

        _report = "crypto core services registered: IAead(AES-GCM), IKeyDerivation(HKDF), ECDH(X25519), KeyPairGenerator, Signature(P-256)";
        return Task.CompletedTask;
    }

    public string GetReport() => _report;
}