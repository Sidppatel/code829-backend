using Contracts.Enums;

namespace Db.Entities;

public class EventTable : BaseEntity
{
    public required string Label { get; set; }
    public int Capacity { get; set; }
    public TableShape Shape { get; set; } = TableShape.Round;
    public string? Color { get; set; }
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public int? RowSpan { get; set; }
    public int? ColSpan { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? TableTemplateId { get; set; }
    public TableTemplate? TableTemplate { get; set; }

    public ICollection<Table> Tables { get; set; } = [];
}
