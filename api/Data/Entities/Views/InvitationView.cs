using Contracts.Enums;

namespace Db.Entities.Views;

public class InvitationView
{
    public Guid InvitationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public AdminRole Role { get; set; }
    public Guid InvitedByBusinessUserId { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string InviterFirstName { get; set; } = string.Empty;
    public string InviterLastName { get; set; } = string.Empty;
}
