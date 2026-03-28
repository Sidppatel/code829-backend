namespace Db.Entities;

/// <summary>
/// A ticket tier/price level for an event. Each event has 2-4 ticket types
/// (e.g., General Admission, VIP, Early Bird). Price stored in cents.
/// </summary>
public class TicketType : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public int QuantityTotal { get; set; }
    public int QuantitySold { get; set; }
    public int SortOrder { get; set; }

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
}
