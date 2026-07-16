# Sample 035 — Crypto-Shredding for the Right to Erasure

Companion to **[Article 035 — Crypto-Shredding for the Right to Erasure](https://relay.nuvoralabs.com/articles/crypto-shredding-right-to-erasure/)**.

An append-only event store never deletes — but GDPR's "right to erasure" requires that a data subject's
PII can be made to disappear. **Crypto-shredding** reconciles the two: encrypt each subject's PII under a
key bound to that subject, and "erase" them by **destroying the key**. The immutable events stay exactly
where they are; their plaintext just becomes permanently unrecoverable.

Everything here lives in `Nuvora.Nexus.Relay.Core.Security` and is **pure and deterministic**:

- **`ICryptoShredder` / `CryptoShredder`** — AES-256-GCM encrypt/decrypt of PII under a subject's key.
  Each value is packed as `nonce | tag | ciphertext` and Base64-encoded; decryption is authenticated.
  `EncryptAsync(subjectId, plaintext)` protects PII (creating the key on first use); `TryDecryptAsync`
  recovers it — or returns `null` once the subject's key has been shredded.
- **`ICryptoKeyStore` / `InMemoryCryptoKeyStore`** — the per-subject AES-256 key store.
  `GetOrCreateKeyAsync` mints a key on first use, `FindKeyAsync` looks one up (or returns `null`), and
  `ShredAsync(subjectId)` destroys it — the erasure operation.

The tests prove the round-trip, the erasure guarantee (after the key is gone the data is `null`), and
per-subject isolation (erasing one subject leaves another untouched).

## Test it

```bash
dotnet test samples/035-crypto-shredding-right-to-erasure/Privacy.CryptoShredding.Tests
```

> Requires the **.NET 10 SDK**. **No database — pure crypto + key store.** No Docker needed.
