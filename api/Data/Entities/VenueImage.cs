namespace Db.Entities;

public class VenueImage : BaseEntity
{
    public required Guid VenueId { get; set; }
    public required Guid ImageId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Venue Venue { get; set; } = null!;
    public Image Image { get; set; } = null!;
}
