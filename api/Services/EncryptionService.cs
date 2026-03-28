using System.Security.Cryptography;
using System.Text;

namespace Api.Services;

/// <summary>
/// AES-256-CBC encryption for AppSettings values.
/// Key is derived from the SETTINGS_ENCRYPTION_KEY env var (64-char hex = 32 bytes).
/// Each encrypted value includes a random 16-byte IV prepended to the ciphertext,
/// both Base64-encoded together.
/// </summary>
public class EncryptionService() : IEncryptionService
{
    private readonly byte[] _key = Convert.FromHexString(
        Environment.GetEnvironmentVariable("SETTINGS_ENCRYPTION_KEY")
            ?? throw new InvalidOperationException("SETTINGS_ENCRYPTION_KEY is required")
    );

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipher, 0, cipher.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant().Trim()));
        return Convert.ToHexStringLower(bytes);
    }
}
