using Contracts.Enums;

namespace Db.Entities.Views;

public class BusinessUserView
{
    public Guid BusinessUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string EmailHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public AdminRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ImageStorageKey { get; set; }
    public string? Phone { get; set; }
    // StripeConnectedAccountId removed when business_users.StripeConnectedAccountId
    // was dropped by DropLegacyStripeOnBusinessUser. Connect data now lives on
    // organizations.StripeConnectedAccountId; query that table directly when needed.
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
