using Contracts.Enums;

namespace Db.Entities;

public class User : BaseEntity
{
    public required string Email { get; set; }
    public required string EmailHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // Address (separate table)
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }

    public string? Phone { get; set; }
    public bool OptInLocationEmail { get; set; }
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>
    /// Stripe Connect account ID for organizers (e.g., "acct_xxx").
    /// Required for organizers to receive payouts via destination charges.
    /// </summary>
    public string? StripeConnectedAccountId { get; set; }
}
