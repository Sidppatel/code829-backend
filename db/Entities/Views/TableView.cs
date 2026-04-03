namespace Db.Entities.Views;

public class TableView
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid VenueId { get; set; }
    public Guid? TableTypeId { get; set; }
    public string Label { get; set; } = null!;
    public int Capacity { get; set; }
    public string Shape { get; set; } = null!;
    public string? Color { get; set; }
    public int PriceCents { get; set; }
    public bool IsActive { get; set; }
    public double PosX { get; set; }
    public double PosY { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
