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
    public DateTime? LastRequestAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public Guid? ImageId { get; set; }
    public Image? Image { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// FK to Organization this BusinessUser belongs to. Permanently nullable —
    /// new BusinessUsers may exist without being attached to any organization
    /// until a developer assigns them via the members UI.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Navigation property to the owning Organization.</summary>
    public Organization? Organization { get; set; }
}
