namespace Db.Entities;

public class BusinessUserEvent : BaseEntity
{
    public Guid BusinessUserId { get; set; }
    public BusinessUser BusinessUser { get; set; } = null!;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? AssignedByBusinessUserId { get; set; }
    public BusinessUser? AssignedByBusinessUser { get; set; }
}
