namespace Db.Entities;

public class PlatformImage : BaseEntity
{
    public required Guid ImageId { get; set; }
    public string? Tag { get; set; }   // e.g. "logo", "hero", "banner", "favicon"
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Image Image { get; set; } = null!;
}
