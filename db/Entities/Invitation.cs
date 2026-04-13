using Contracts.Enums;

namespace Db.Entities;

public class Invitation : BaseEntity
{
    public required string Email { get; set; }
    public required string TokenHash { get; set; }
    public AdminRole Role { get; set; }
    public Guid InvitedByAdminUserId { get; set; }
    public AdminUser InvitedBy { get; set; } = null!;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
