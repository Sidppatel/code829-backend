using Contracts.Enums;

namespace Db.Entities;

public class AdminUser : BaseEntity
{
    public required string Email { get; set; }
    public required string EmailHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string PasswordHash { get; set; }
    public AdminRole Role { get; set; } = AdminRole.Staff;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public Guid? ImageId { get; set; }
    public Image? Image { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// Stripe Connect account ID for organizers (e.g., "acct_xxx").
    /// Required for organizers to receive payouts via destination charges.
    /// </summary>
    public string? StripeConnectedAccountId { get; set; }
}
