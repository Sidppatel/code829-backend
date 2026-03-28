namespace Db.Entities;

/// <summary>
/// Stores application settings as encrypted key-value pairs.
/// Values are AES-256 encrypted before storage and decrypted on read.
/// Cached in Redis with 30-second TTL.
/// </summary>
public class AppSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string EncryptedValue { get; set; }
    public string? Description { get; set; }
}
