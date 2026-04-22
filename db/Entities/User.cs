namespace Db.Entities;

public class User : BaseEntity
{
    public required string Email { get; set; }
    public required string EmailHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    /// <summary>
    /// Bcrypt/argon password hash for traditional email+password auth.
    /// Nullable because pre-existing magic-link-only users have no password set.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// True once the user has confirmed ownership of their email via verification link.
    /// Default false for new email+password signups; magic-link flow may set this implicitly.
    /// </summary>
    public bool EmailVerified { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // Address (separate table)
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }

    public string? Phone { get; set; }
    public bool OptInLocationEmail { get; set; }
    public bool HasCompletedOnboarding { get; set; }

    public Guid? ImageId { get; set; }
    public Image? Image { get; set; }
}
