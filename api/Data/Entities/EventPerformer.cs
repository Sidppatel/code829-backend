namespace Db.Entities;

public class EventPerformer
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid PerformerId { get; set; }
    public Performer Performer { get; set; } = null!;

    public int SortOrder { get; set; }
    public string EventMeta { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
