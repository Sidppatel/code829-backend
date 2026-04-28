using Contracts.Enums;

namespace Db.Entities;

public class Table : BaseEntity
{
    public required string Label { get; set; }
    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public TableStatus Status { get; set; } = TableStatus.Available;
    public Guid? LockedByUserId { get; set; }
    public User? LockedByUser { get; set; }
    public DateTime? LockExpiresAt { get; set; }

    public Guid EventTableId { get; set; }
    public EventTable EventTable { get; set; } = null!;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
}
