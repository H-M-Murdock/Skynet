# Kryptografie-/Lizenz-Glossar (Skynet.Core)

Dieses Dokument erklärt verwendete Abkürzungen, Begriffe und Abläufe. Es dient als Referenz für Entwickler und Reviewer.

---

## AAD/HKDF – Projektweite Konventionen (verbindlich)

- HKDF-Info
  - Format: UTF-8 "tenant/{TenantId}/license-v1"
  - Zweck: Bindet den abgeleiteten Sitzungsschlüssel K an Tenant und Version der Lizenz-Policy.
- HKDF-Salt
  - Format: NonceClient || NonceServer (Konkatenation in genau dieser Reihenfolge)
  - Zweck: Entkoppelt Schlüsselableitungen bei neuen Handshakes; erhöht Unvorhersagbarkeit.
- AEAD-AAD
  - Format: UTF-8 "tenant/{TenantId};purpose={Purpose};v=1"
  - Zweck: Bindet Ciphertexte (z. B. DEK-Wrap, Secrets-at-rest) an Tenant und fachlichen Zweck.
  - Beispiele:
    - "tenant/85b28ec9-34db-414b-9e29-2c4ae17a6923;purpose=license;v=1"
    - "tenant/{id};purpose=secret.profile;v=1"

Diese Konstanten sind auf Client und Server identisch zu verwenden. Abweichungen führen zu Verifikationsfehlern.

---

## Abkürzungen und Begriffe

- AES (Advanced Encryption Standard)
  Symmetrischer Blockchiffre‑Standard (128‑Bit Blockgröße). Typische Schlüsselgrößen: 128/192/256 Bit.

- GCM (Galois/Counter Mode)
  Betriebsmodus für Blockchiffren wie AES. Bietet Vertraulichkeit und Integrität in einem (AEAD). Nutzt Nonce (IV) + Counter.

- AES‑GCM
  AES im GCM‑Modus. In .NET über AesGcm nutzbar. Empfohlene Parameter: Nonce = 12 Bytes, Tag = 16 Bytes.

- AEAD (Authenticated Encryption with Associated Data)
  Verschlüsselungsverfahren, das gleichzeitig Vertraulichkeit (Ciphertext) und Integrität/Authentizität (Tag) liefert. “Associated Data” (AAD) wird mit authentifiziert, aber nicht verschlüsselt (z. B. TenantId, Policy).

- AAD (Additional Authenticated Data)
  Kontextdaten, die in die AEAD‑Authentifizierung einfließen (nicht verschlüsselt, aber “mit signiert”). Bindet Ciphertext an Kontext (z. B. Tenant/Policy). Beispiel: “tenant/{TenantId}”.

- ECDH (Elliptic Curve Diffie–Hellman)
  Schlüsselaustausch auf elliptischen Kurven. Erzeugt ein gemeinsames Geheimnis (Shared Secret), aus dem symmetrische Schlüssel abgeleitet werden. Beispiel: X25519.

- X25519
  Moderne ECDH‑Kurve (Montgomery‑Form) über Curve25519. 32‑Byte Keys, schnell und weit verbreitet. In .NET typischerweise über externe Libs (NaCl/NSec) oder via Interop.

- HKDF (HMAC‑based Key Derivation Function)
  Key‑Derivation‑Funktion nach RFC 5869. Leitet aus IKM (z. B. ECDH‑Shared‑Secret) Schlüsselmaterial ab (KEK/SessionKey). Üblich: HKDF‑SHA256.

- HMAC (Hash‑based Message Authentication Code)
  Keyed Hash (z. B. HMAC‑SHA256) – Grundlage für HKDF‑Extract/Expand.

- KEK (Key Encryption Key)
  Symmetrischer Schlüssel, der zum Einhüllen (Wrap) anderer Schlüssel (z. B. DEKs) verwendet wird. In unserem Design: aus ECDH+HKDF abgeleiteter Sitzungsschlüssel K.

- DEK (Data Encryption Key)
  Symmetrischer Schlüssel, mit dem Nutzdaten/Secrets verschlüsselt werden. DEK wird clientseitig erzeugt und bleibt im Klartext nur beim Client (Zero‑Knowledge).

- Wrap/Unwrap
  “Einpacken/Auspacken” eines DEK mit einem KEK (AEAD). Ausgabeformat z. B. nonce|tag|cipher (konkateniert).

- ETag/Fingerprint
  Stabiler Fingerabdruck eines Inhalts (z. B. SHA‑256‑Hex). Hilft bei Cache/Diagnostik/Versionierung. Bei Zertifikaten kann der Thumbprint als ETag dienen.

- LicenseEnvelope
  Kryptografische/transportbezogene Hülle einer Lizenz:
  - TenantId, ServerPubEcdh, NonceClient, NonceServer
  - Gültigkeitsfenster (Issued/NotBefore/Expires)
  - Algorithmen (KEM/KDF/AEAD)
  - optionale SignKeyId (kid)
  - Signature (Server‑Signatur über alle Felder außer Signature selbst)
  Strikte Variante: enthält keinen DEKenc.

- LicenseInfo
  Domänenobjekt der “ausgewerteten, verifizierten” Lizenz (fachlich):
  - Zeitfenster, Claims (Feature‑Flags/Limits)
  - Status der Signaturprüfung (SignatureValid)
  - optional: Envelope‑Fingerprint, Raw‑License für Audit

- Zero‑Knowledge (ZK)
  Der Server kennt den DEK nie (weder at rest noch zur Laufzeit). Der Client generiert und verpackt DEK jederzeit selbst, der Server liefert nur die verifizierbare Hülle (Envelope).

- Rekey / ReKeying
  Rotation des DEK. Betrifft alle at‑rest Secrets:
  - Dual‑Read/Single‑Write: Lesen ggf. mit altem DEK, sofort mit neuem DEK neu schreiben; neues Schreiben nur mit neuem DEK.
  - Hintergrundmigration optional.
  - Ergebnis als Statistik (migrated/failed/skipped).

- kid (Key Id / SignKeyId)
  Kennung eines Signatur‑Schlüssels (Rotation/Mehrschlüsselbetrieb). Erleichtert Auswahl des richtigen Verify‑Keys.

---

## Typische Parameter und Längen

- AES: Key = 16/24/32 Bytes (AES‑128/192/256)
- AES‑GCM:
  - Nonce/IV = 12 Bytes (96 Bit)
  - Auth‑Tag = 16 Bytes (128 Bit)
- X25519:
  - Private/Public Key je 32 Bytes
  - Shared Secret (IKM) 32 Bytes
- HKDF:
  - HKDF‑SHA256 → HashLen = 32
  - Salt/Info variabel (leer erlaubt, Salt empfohlen)

---

## Strikter Zero‑Knowledge‑Handshake (2 Nachrichten)

Ziel: Der Server signiert eine Hülle, der Client leitet daraus einen Sitzungsschlüssel K ab und verpackt seinen DEK lokal. Der Server sieht den DEK nie.

ASCII‑Fluss:

Client Server |-- PubC, TenantMeta, NonceC --------->| | | | (ephemeral PrivS/PubS, NonceS) |<-- Envelope(TenantId, PubS, NonceS, | | Iss/Exp/Alg, SigS) -------| | | K = HKDF(ECDH(PrivC, PubS), salt=NonceC||NonceS, info="tenant/{id}/license-v1") | DEK = Random(32B) | DEKenc = AEAD_Encrypt(K, DEK, AAD="tenant/{id}") | LicenseInfo (Claims/Zeiten/Validierung) erzeugen und speichern

Eigenschaften:
- Envelope enthält keine DEK‑Daten (strikt). Signatur sichert Integrität/Herkunft.
- Client speichert DEKenc lokal (nicht Teil der Envelope).
- Rekey/Rotation wird durch neuen K (neue Envelope) oder neuen DEK erreicht.

---

## AEAD‑Datenformat (empfohlen)

Für DEK‑Wrap und Secrets at rest:

wrapped = nonce (12B) || tag (16B) || cipher (N)

- nonce: zufällig pro Verschlüsselung (RandomNumberGenerator)
- tag: 128 Bit Auth‑Tag
- cipher: Chiffretext (AEAD‑GCM Output)
- AAD (optional) z. B. “tenant/{id}” oder Policy‑Hash

---

## Kanonisierung (Canonicalization)

Server und Client müssen dieselben Bytes signieren/verifizieren. ILicenseCanonicalizer definiert die “Ohne‑Signature”‑Serialisierung (z. B. deterministisches JSON):
- Alphabetische Property‑Reihenfolge
- Byte‑Arrays als Base64
- ISO‑8601 Zeitformat ("O")
- FeatureFlags lexikografisch sortieren

So entsteht ein stabiler Signatur‑Input.

---

## Zeitfenster‑Prüfung und Grace

- Gültigkeit: NotBeforeUtc ≤ now ≤ ExpiresUtc
- Grace‑Period optional, um harte Ausfälle bei geringer Uhrdrift zu vermeiden
- IClock verwenden (testbar), nicht DateTime.UtcNow direkt

---

## Fehlerklassen

- ArgumentException:
    - Ungültige Key‑/Nonce‑/Tag‑Längen
    - Null/empty‑Inputs
- CryptographicException:
    - Ungültige Signatur
    - AEAD‑Tag‑Verifikation fehlgeschlagen
    - Lizenz außerhalb des Gültigkeitsfensters (je nach Policy)

---

## Best Practices

- Client/Server strikt trennen:
    - Client: nur Verify‑PublicKey, eigener ECDH‑PrivateKey, AEAD‑/HKDF‑Implementierungen
    - Server: Sign‑PrivateKey (HSM/KMS), Envelope‑Issuer
- Keine Secrets/Server‑Keys im Client‑Build
- HKDF‑Info mit TenantId binden: “tenant/{id}/license‑vX”
- AAD konsequent nutzen (Tenant/Policy in AEAD binden)
- Rotation/ReKey frühzeitig planen (Operationsfähig: Statistik, Retry, Resume)

---

## Beispielkonfiguration

- KemAlg: X25519
- KdfAlg: HKDF‑SHA256
- AeadAlg: AES‑256‑GCM
- Nonce: 12B, Tag: 16B
- DEK: 32B
- Hash/ETag: SHA‑256 (Hex)

---

## Kurzer Code‑Ausschnitt (Client)

csharp // Shared secret (ECDH) var ikm = ecdh.DeriveSharedSecret(privC, envelope.ServerPubEcdh);
// Session key (K) var salt = Concat(envelope.NonceClient, envelope.NonceServer); var info = Encoding.UTF8.GetBytes($"tenant/{envelope.TenantId}/license-v1"); var K = hkdf.HkdfSha256(ikm, salt, info, 32);
// DEK erzeugen und verpacken var dek = dekMgr.GenerateDek(32); var aad = Encoding.UTF8.GetBytes($"tenant/{envelope.TenantId}"); var dekEnc = dekMgr.WrapDek(K, dek, aad);
// Secret at rest verschlüsseln var (n, t, c) = aead.Encrypt(dek, plaintext, aad);

---

## Checkliste (Produktionsbetrieb)

- [ ] Verify-Key (Server‑Sign‑PublicKey) im Client gepflegt/rotierbar (kid).
- [ ] ILicenseCanonicalizer auf Server und Client identisch.
- [ ] Nonce/Tag/Key‑Längen strikt geprüft.
- [ ] AAD gesetzt (Tenant/Policy).
- [ ] Grace/Operations (ReKey‑Statistik, Fehlerhandling) definiert.
- [ ] Secrets/Keys niemals im Log.