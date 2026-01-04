using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using XcluadeAgent.Core.Models;

namespace XcluadeAgent.Infrastructure.Security;

/// <summary>
/// Service for encrypting and decrypting sensitive tokens
/// Uses AES-256-GCM for authenticated encryption
/// </summary>
public interface ITokenEncryptionService
{
    /// <summary>
    /// Encrypt a plaintext token
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypt an encrypted token
    /// </summary>
    string? Decrypt(string ciphertext);

    /// <summary>
    /// Check if a string is encrypted (has the encryption prefix)
    /// </summary>
    bool IsEncrypted(string? value);
}

/// <summary>
/// AES-256-GCM encryption service for tokens
/// </summary>
public class TokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;
    private const string EncryptionPrefix = "enc:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public TokenEncryptionService(IOptions<SecurityConfig> securityConfig)
    {
        // Derive a 256-bit key from the JWT secret using SHA256
        var secret = securityConfig.Value.JwtSecret;
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JWT secret is not configured");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext;
        }

        // Don't double-encrypt
        if (IsEncrypted(plaintext))
        {
            return plaintext;
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Combine nonce + ciphertext + tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return EncryptionPrefix + Convert.ToBase64String(result);
    }

    /// <inheritdoc/>
    public string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return ciphertext;
        }

        // If not encrypted, return as-is (for backward compatibility)
        if (!IsEncrypted(ciphertext))
        {
            return ciphertext;
        }

        try
        {
            var encryptedData = Convert.FromBase64String(ciphertext[EncryptionPrefix.Length..]);

            if (encryptedData.Length < NonceSize + TagSize)
            {
                return null;
            }

            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertextBytes = new byte[encryptedData.Length - NonceSize - TagSize];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(encryptedData, NonceSize, ciphertextBytes, 0, ciphertextBytes.Length);
            Buffer.BlockCopy(encryptedData, NonceSize + ciphertextBytes.Length, tag, 0, TagSize);

            var plaintextBytes = new byte[ciphertextBytes.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertextBytes, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (CryptographicException)
        {
            // Decryption failed - token may be corrupted or tampered
            return null;
        }
        catch (FormatException)
        {
            // Invalid base64
            return null;
        }
    }

    /// <inheritdoc/>
    public bool IsEncrypted(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptionPrefix);
    }
}
