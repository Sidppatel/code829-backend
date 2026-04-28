namespace Db.Entities;

/// <summary>
/// Stores non-sensitive application settings as key-value pairs.
/// Cached in Redis with 30-second TTL. Secrets are stored in environment variables, not here.
/// </summary>
public class AppSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
}
