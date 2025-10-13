using System.Text;
using System.Text.Json;

namespace Skynet.Core.Licensing;

/// <summary>
/// Einfache kanonische JSON-Serialisierung:
/// - UTF-8 ohne BOM
/// - Property-Namen in fester Reihenfolge
/// - FeatureFlags alphabetisch sortiert
/// - Datumswerte als ISO-8601 ("O")
/// - Bytefelder Base64
/// Signature wird NICHT eingebettet (wird extern verifiziert).
/// </summary>
public sealed class JsonLicenseCanonicalizer : ILicenseCanonicalizer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = false
    };

    public byte[] CanonicalizeWithoutSignature(LicenseEnvelope e, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, WriterOptions))
        {
            writer.WriteStartObject();

            // Feste Reihenfolge der Properties (ohne Signature):
            writer.WriteString("tenantId", e.TenantId.ToString());
            writer.WriteString("kemAlg", e.KemAlg.ToString());
            writer.WriteString("kdfAlg", e.KdfAlg.ToString());
            writer.WriteString("aeadAlg", e.AeadAlg.ToString());
            if (!string.IsNullOrEmpty(e.SignKeyId))
                writer.WriteString("signKeyId", e.SignKeyId);

            writer.WriteString("issuedAtUtc", e.IssuedAtUtc.UtcDateTime.ToString("O"));
            if (e.NotBeforeUtc.HasValue)
                writer.WriteString("notBeforeUtc", e.NotBeforeUtc.Value.UtcDateTime.ToString("O"));
            writer.WriteString("expiresUtc", e.ExpiresUtc.UtcDateTime.ToString("O"));

            writer.WriteString("serverPubEcdh", Convert.ToBase64String(e.ServerPubEcdh));
            writer.WriteString("nonceClient", Convert.ToBase64String(e.NonceClient));
            writer.WriteString("nonceServer", Convert.ToBase64String(e.NonceServer));

            // FeatureFlags deterministisch sortieren
            writer.WritePropertyName("featureFlags");
            writer.WriteStartObject();
            if (e.FeatureFlags is { Count: > 0 })
            {
                foreach (var kv in e.FeatureFlags.OrderBy(k => k.Key, StringComparer.Ordinal))
                    writer.WriteString(kv.Key, kv.Value);
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return ms.ToArray();
    }
}
