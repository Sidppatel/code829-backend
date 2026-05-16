using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Db.Entities;

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
