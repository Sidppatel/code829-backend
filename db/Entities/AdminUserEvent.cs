namespace Db.Entities;

public class AdminUserEvent : BaseEntity
{
    public Guid AdminUserId { get; set; }
    public AdminUser AdminUser { get; set; } = null!;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? AssignedByAdminUserId { get; set; }
    public AdminUser? AssignedByAdminUser { get; set; }
}
