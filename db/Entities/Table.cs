using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// A placed table instance for an event's floor plan. Supports both grid mode
/// (GridRow/GridCol) and canvas mode (PosX/PosY). Tables are event-scoped
/// but retain a VenueId reference for backward compatibility with Phase 4.
/// </summary>
public class Table : BaseEntity
{
    public required string Label { get; set; }
    public int Capacity { get; set; }
    public TableShape Shape { get; set; } = TableShape.Round;
    public string? Color { get; set; }
    public string? Section { get; set; }
    public PriceType PriceType { get; set; } = PriceType.PerSeat;
    public int PriceCents { get; set; }
    public int? PriceOverrideCents { get; set; }
    public bool IsActive { get; set; } = true;

    // Grid mode positioning
    public int? GridRow { get; set; }
    public int? GridCol { get; set; }

    // Canvas mode positioning
    public double? PosX { get; set; }
    public double? PosY { get; set; }
    public double Width { get; set; } = 80;
    public double Height { get; set; } = 80;
    public double Rotation { get; set; }
    public int SortOrder { get; set; }

    public Guid? TableTypeId { get; set; }
    public TableType? TableType { get; set; }

    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public ICollection<Seat> Seats { get; set; } = [];
}
