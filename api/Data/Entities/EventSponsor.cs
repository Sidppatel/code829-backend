namespace Db.Entities;

public class EventSponsor
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid SponsorId { get; set; }
    public Sponsor Sponsor { get; set; } = null!;

    public int SortOrder { get; set; }
    public string EventMeta { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
