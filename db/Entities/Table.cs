using Contracts.Enums;

namespace Db.Entities;

public class Table : BaseEntity
{
    public required string Label { get; set; }
    public int Capacity { get; set; }
    public TableShape Shape { get; set; } = TableShape.Round;
    public string? Color { get; set; }
    public int PriceCents { get; set; }
    public bool IsActive { get; set; } = true;

    public TableStatus Status { get; set; } = TableStatus.Available;
    public Guid? LockedByUserId { get; set; }
    public User? LockedByUser { get; set; }
    public DateTime? LockExpiresAt { get; set; }

    public double PosX { get; set; }
    public double PosY { get; set; }

    public int SortOrder { get; set; }

    public Guid? TableTypeId { get; set; }
    public TableType? TableType { get; set; }

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
}
