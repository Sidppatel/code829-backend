using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Db.Entities;

[Table("admin_password_reset_tokens")]
public class AdminPasswordResetToken : BaseEntity
{
    [Required]
    public Guid AdminUserId { get; set; }

    [ForeignKey(nameof(AdminUserId))]
    public AdminUser AdminUser { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = null!;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }
}
