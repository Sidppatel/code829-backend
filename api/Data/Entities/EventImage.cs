namespace Db.Entities;

public class EventImage : BaseEntity
{
    public required Guid EventId { get; set; }
    public required Guid ImageId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Event Event { get; set; } = null!;
    public Image Image { get; set; } = null!;
}
