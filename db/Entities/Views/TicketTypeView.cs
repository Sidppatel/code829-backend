namespace Db.Entities.Views;

/// <summary>
/// Read-only view entity mapping to v_ticket_types.
/// Resolves COALESCE defaults from ticket_type_templates.
/// </summary>
public class TicketTypeView
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public int PlatformFeeCents { get; set; }
    public int QuantityTotal { get; set; }
    public int QuantitySold { get; set; }
    public int SortOrder { get; set; }
    public Guid? TemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
