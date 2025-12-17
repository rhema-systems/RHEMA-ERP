using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

public interface ICryptoService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

public class CryptoService : ICryptoService
{
    private readonly string _encryptionKey;
    private readonly ILogger<CryptoService> _logger;

    public CryptoService(IConfiguration configuration, ILogger<CryptoService> logger)
    {
        _encryptionKey = configuration["Security:EncryptionKey"] ??
                        throw new InvalidOperationException("Security:EncryptionKey is not configured");
        _logger = logger;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        try
        {
            using var aes = Aes.Create();
            var key = DeriveKey(_encryptionKey, aes.KeySize / 8);
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var msEncrypt = new MemoryStream();

            // Prepend IV to the encrypted data
            msEncrypt.Write(aes.IV, 0, aes.IV.Length);

            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(plainText);
            }

            return Convert.ToBase64String(msEncrypt.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        // Check if the string is likely already plain text (not base64 encrypted)
        if (!IsLikelyEncrypted(cipherText))
        {
            _logger.LogDebug("Password appears to be plain text, returning as-is");
            return cipherText;
        }

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            var key = DeriveKey(_encryptionKey, aes.KeySize / 8);
            aes.Key = key;

            // Extract IV from the beginning of the cipher text
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aes.IV = iv;

            // Get the actual cipher text without IV
            var cipher = new byte[fullCipher.Length - iv.Length];
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            throw new InvalidOperationException("Decryption failed", ex);
        }
    }

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        try
        {
            // Generate a salt
            var salt = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password with PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);

            // Combine salt and hash
            var hashBytes = new byte[64];
            Array.Copy(salt, 0, hashBytes, 0, 32);
            Array.Copy(hash, 0, hashBytes, 32, 32);

            return Convert.ToBase64String(hashBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hash password");
            throw new InvalidOperationException("Password hashing failed", ex);
        }
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        try
        {
            var hashBytes = Convert.FromBase64String(hashedPassword);

            // Extract salt
            var salt = new byte[32];
            Array.Copy(hashBytes, 0, salt, 0, 32);

            // Extract hash
            var hash = new byte[32];
            Array.Copy(hashBytes, 32, hash, 0, 32);

            // Verify password
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            var testHash = pbkdf2.GetBytes(32);

            return hash.SequenceEqual(testHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify password");
            return false;
        }
    }

    private static byte[] DeriveKey(string password, int keyLength)
    {
        // Use a fixed salt for consistent key derivation - in production, this should be more sophisticated
        var salt = Encoding.UTF8.GetBytes("ErpSystemSalt2024");
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyLength);
    }

    private static bool IsLikelyEncrypted(string text)
    {
        // A simple heuristic: encrypted values are base64 and long enough to include IV + cipher
        // Try to base64-decode and ensure length is reasonable (> 16 for IV)
        try
        {
            var bytes = Convert.FromBase64String(text);
            return bytes.Length > 16; // at least IV length
        }
        catch
        {
            return false;
        }
    }
}
