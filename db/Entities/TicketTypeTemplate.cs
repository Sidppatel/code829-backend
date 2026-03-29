namespace Db.Entities;

/// <summary>
/// Reusable ticket tier template (e.g., General Admission, VIP, Early Bird).
/// Events reference these via TicketType.TemplateId; NULL overrides fall back to template defaults.
/// </summary>
public class TicketTypeTemplate : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int DefaultPriceCents { get; set; }
    public int DefaultPlatformFeeCents { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<TicketType> TicketTypes { get; set; } = [];
}
