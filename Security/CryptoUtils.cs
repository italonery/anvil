using System.Security.Cryptography;
using System.Text;

namespace Anvil.Security;

public static class CryptoUtils
{
    private const int _saltSize = 16;
    private const int _nonceSize = 12;
    private const int _tagSize = 16;
    private const int _keySize = 32;
    private const int _pbkdf2Iterations = 600_000;
    private const byte _payloadVersion = 1;

    // Output format (all bytes, then Base64): [v:1][salt:16][nonce:12][tag:16][cipher:n]
    public static string EncryptToBase64(string plainText, string key)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("Plain text is required.", nameof(plainText));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Encryption key is required.", nameof(key));
        }

        var salt = RandomNumberGenerator.GetBytes(_saltSize);
        var nonce = RandomNumberGenerator.GetBytes(_nonceSize);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(key, salt, _pbkdf2Iterations, HashAlgorithmName.SHA256, _keySize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tagBytes = new byte[_tagSize];

        try
        {
            using (var aes = new AesGcm(derivedKey, _tagSize))
            {
                aes.Encrypt(nonce, plainBytes, cipherBytes, tagBytes);
            }

            var payload = new byte[1 + _saltSize + _nonceSize + _tagSize + cipherBytes.Length];
            payload[0] = _payloadVersion;
            Buffer.BlockCopy(salt, 0, payload, 1, _saltSize);
            Buffer.BlockCopy(nonce, 0, payload, 1 + _saltSize, _nonceSize);
            Buffer.BlockCopy(tagBytes, 0, payload, 1 + _saltSize + _nonceSize, _tagSize);
            Buffer.BlockCopy(cipherBytes, 0, payload, 1 + _saltSize + _nonceSize + _tagSize, cipherBytes.Length);

            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public static string DecryptFromBase64(string payloadBase64, string key)
    {
        if (string.IsNullOrWhiteSpace(payloadBase64))
        {
            throw new ArgumentException("Encrypted payload is required.", nameof(payloadBase64));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Encryption key is required.", nameof(key));
        }

        var payload = Convert.FromBase64String(payloadBase64);
        var minLength = 1 + _saltSize + _nonceSize + _tagSize;
        if (payload.Length <= minLength)
        {
            throw new ArgumentException("Invalid encrypted payload.", nameof(payloadBase64));
        }

        if (payload[0] != _payloadVersion)
        {
            throw new ArgumentException("Unsupported encrypted payload version.", nameof(payloadBase64));
        }

        var salt = new byte[_saltSize];
        var nonce = new byte[_nonceSize];
        var tag = new byte[_tagSize];
        var cipher = new byte[payload.Length - minLength];

        Buffer.BlockCopy(payload, 1, salt, 0, _saltSize);
        Buffer.BlockCopy(payload, 1 + _saltSize, nonce, 0, _nonceSize);
        Buffer.BlockCopy(payload, 1 + _saltSize + _nonceSize, tag, 0, _tagSize);
        Buffer.BlockCopy(payload, minLength, cipher, 0, cipher.Length);

        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(key, salt, _pbkdf2Iterations, HashAlgorithmName.SHA256, _keySize);
        var plainBytes = new byte[cipher.Length];

        try
        {
            using (var aes = new AesGcm(derivedKey, _tagSize))
            {
                aes.Decrypt(nonce, cipher, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }
}
