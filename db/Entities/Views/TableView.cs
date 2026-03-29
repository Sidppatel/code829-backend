namespace Db.Entities.Views;

/// <summary>
/// Read-only view entity mapping to v_tables.
/// Resolves COALESCE defaults from table_types.
/// </summary>
public class TableView
{
    public Guid Id { get; set; }
    public Guid? EventId { get; set; }
    public Guid VenueId { get; set; }
    public Guid? TableTypeId { get; set; }
    public string Label { get; set; } = null!;
    public int Capacity { get; set; }
    public string Shape { get; set; } = null!;
    public string? Color { get; set; }
    public string? Section { get; set; }
    public string PriceType { get; set; } = null!;
    public int EffectivePriceCents { get; set; }
    public int PlatformFeeCents { get; set; }
    public bool IsActive { get; set; }
    public int? GridRow { get; set; }
    public int? GridCol { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
