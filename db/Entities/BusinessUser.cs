using Contracts.Enums;

namespace Db.Entities;

public class BusinessUser : BaseEntity
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
    /// <remarks>
    /// DEPRECATED — superseded by Organization.StripeConnectedAccountId.
    /// Kept temporarily so the BackfillOrganizationsFromBusinessUsers migration
    /// can copy the value over; the column is dropped by
    /// DropLegacyStripeOnBusinessUser (Migration 3) once the backfill runs.
    /// </remarks>
    public string? StripeConnectedAccountId { get; set; }

    /// <summary>
    /// FK to Organization this BusinessUser belongs to. Permanently nullable —
    /// new BusinessUsers may exist without being attached to any organization
    /// until a developer assigns them via the members UI.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Navigation property to the owning Organization.</summary>
    public Organization? Organization { get; set; }
}
