using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Db.Entities;

/// <summary>
/// Stores hashed email-verification tokens emitted during email+password signup.
/// Raw token is SHA-256 hashed (hex) before storage; never persisted in plaintext.
/// Single-use: <see cref="UsedAt"/> is set on consumption, at which point the parent
/// User is flipped to <c>EmailVerified = true</c> in the same transaction.
/// </summary>
[Table("user_email_verification_tokens")]
public class UserEmailVerificationToken : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = null!;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}
