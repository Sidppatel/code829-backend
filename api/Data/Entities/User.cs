namespace Db.Entities;

public class User : BaseEntity
{
    public required string Email { get; set; }
    public required string EmailHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    public string? PasswordHash { get; set; }

    public bool EmailVerified { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }

    public string? Phone { get; set; }
    public bool OptInLocationEmail { get; set; }
    public bool HasCompletedOnboarding { get; set; }

    public Guid? ImageId { get; set; }
    public Image? Image { get; set; }

    public string? GoogleSubject { get; set; }
}
