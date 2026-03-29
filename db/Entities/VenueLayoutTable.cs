using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// Default table placement within a venue layout. When an event uses this layout,
/// tables are cloned from these defaults. Individual tables can override via nullable columns.
/// </summary>
public class VenueLayoutTable : BaseEntity
{
    public required string Label { get; set; }
    public string? Section { get; set; }
    public int? GridRow { get; set; }
    public int? GridCol { get; set; }
    public int SortOrder { get; set; }
    public PriceType PriceType { get; set; } = PriceType.PerSeat;
    public int PriceCents { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid VenueLayoutId { get; set; }
    public VenueLayout VenueLayout { get; set; } = null!;

    public Guid? TableTypeId { get; set; }
    public TableType? TableType { get; set; }

    public ICollection<Table> Tables { get; set; } = [];
}
