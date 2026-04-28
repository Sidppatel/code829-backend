namespace Db.Entities;

/// <summary>
/// Stores hashed magic link tokens for passwordless authentication.
/// Tokens are cryptographically random (32 bytes), hashed with SHA-256 before storage.
/// Single-use: consumed on verification. Expires after configurable minutes (default 15).
/// </summary>
public class MagicLinkToken : BaseEntity
{
    public required string TokenHash { get; set; }
    public required string Email { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
}
