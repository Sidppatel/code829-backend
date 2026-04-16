using System.Security.Cryptography;
using System.Text;

namespace Api.Services;

/// <summary>
/// Provides SHA-256 email hashing for privacy-preserving lookups.
/// Encryption/decryption removed — secrets now live in environment variables via ISecretsProvider.
/// </summary>
public class EncryptionService : IEncryptionService
{
    public string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant().Trim()));
        return Convert.ToHexStringLower(bytes);
    }
}
