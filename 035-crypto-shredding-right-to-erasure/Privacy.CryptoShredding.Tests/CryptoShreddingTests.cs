using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Security;
using Xunit;

namespace Privacy.CryptoShredding;

/// <summary>
/// Crypto-shredding implements the GDPR "right to erasure" over an <em>append-only</em> event store
/// without rewriting immutable events: PII is encrypted under a per-subject key (<see cref="CryptoShredder"/>
/// over an <see cref="InMemoryCryptoKeyStore"/>), and "erasing" a subject means destroying that key
/// (<see cref="ICryptoKeyStore.ShredAsync"/>). The events stay exactly where they are; their plaintext
/// just becomes unrecoverable.
///
/// These tests prove the four properties that make the technique trustworthy:
/// (a) protecting PII yields ciphertext that differs from the plaintext;
/// (b) while the key exists, unprotecting recovers the original plaintext;
/// (c) erasing the subject's key makes their data unrecoverable (decrypt returns <c>null</c>);
/// (d) erasing one subject does not affect another subject's data.
/// </summary>
public sealed class CryptoShreddingTests
{
    private static (CryptoShredder Shredder, InMemoryCryptoKeyStore Keys) Create()
    {
        var keys = new InMemoryCryptoKeyStore();
        return (new CryptoShredder(keys), keys);
    }

    [Fact]
    public async Task Protecting_pii_produces_ciphertext_that_differs_from_the_plaintext()
    {
        var (shredder, _) = Create();

        var cipher = await shredder.EncryptAsync("subject-X", "Ada Lovelace, ada@example.com");

        cipher.Should().NotBe("Ada Lovelace, ada@example.com",
            "the stored value must be encrypted, never the raw PII");
        cipher.Should().NotContain("Ada Lovelace",
            "no fragment of the plaintext should survive in the ciphertext");
    }

    [Fact]
    public async Task Unprotecting_while_the_key_exists_recovers_the_original_plaintext()
    {
        var (shredder, _) = Create();

        var cipher = await shredder.EncryptAsync("subject-X", "Ada Lovelace, ada@example.com");
        var recovered = await shredder.TryDecryptAsync("subject-X", cipher);

        recovered.Should().Be("Ada Lovelace, ada@example.com",
            "while the subject's key exists, their data round-trips exactly");
    }

    [Fact]
    public async Task Erasing_the_subjects_key_makes_their_data_unrecoverable()
    {
        var (shredder, keys) = Create();
        var cipher = await shredder.EncryptAsync("subject-X", "Ada Lovelace, ada@example.com");

        // The "right to erasure": we never touch the immutable event log — we destroy the key.
        await keys.ShredAsync("subject-X");

        (await shredder.TryDecryptAsync("subject-X", cipher)).Should().BeNull(
            "destroying the subject's key erases the data irrecoverably, without rewriting any event");
        (await keys.FindKeyAsync("subject-X")).Should().BeNull(
            "the key itself is gone, so no later process can ever decrypt the ciphertext");
    }

    [Fact]
    public async Task Erasing_one_subject_does_not_affect_another_subjects_data()
    {
        var (shredder, keys) = Create();
        var cipherX = await shredder.EncryptAsync("subject-X", "x-pii");
        var cipherY = await shredder.EncryptAsync("subject-Y", "y-pii");

        await keys.ShredAsync("subject-X");

        (await shredder.TryDecryptAsync("subject-X", cipherX)).Should().BeNull(
            "subject-X exercised their right to erasure");
        (await shredder.TryDecryptAsync("subject-Y", cipherY)).Should().Be("y-pii",
            "per-subject keys isolate erasure — subject-Y is untouched");
    }
}
