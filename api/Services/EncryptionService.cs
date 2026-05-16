using System.Security.Cryptography;
using System.Text;

namespace Api.Services;

public class EncryptionService : IEncryptionService
{
    public string HashEmail(string email)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant().Trim()));
        return Convert.ToHexStringLower(bytes);
    }
}
