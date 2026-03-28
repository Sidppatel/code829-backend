using Contracts.Enums;

namespace Db.Entities;

public class User : BaseEntity
{
    public required string Email { get; set; }
    public required string EmailHash { get; set; }
    public required string Name { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}
