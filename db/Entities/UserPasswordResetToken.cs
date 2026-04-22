using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Db.Entities;

/// <summary>
/// Stores hashed password-reset tokens for traditional email+password User accounts.
/// Raw token is SHA-256 hashed (hex) before storage; never persisted in plaintext.
/// Single-use: <see cref="UsedAt"/> is set on consumption.
/// </summary>
[Table("user_password_reset_tokens")]
public class UserPasswordResetToken : BaseEntity
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
