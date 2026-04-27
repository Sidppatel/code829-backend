namespace Db.Entities;

public class Image : BaseEntity
{
    public required string EntityType { get; set; }   // "venue", "event", "user", "platform"
    public required Guid EntityId { get; set; }
    public required string StorageKey { get; set; }   // R2 object key (without variant suffix)
    public string? OriginalName { get; set; }
    public int SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int SortOrder { get; set; }
    public Guid? UploadedById { get; set; }
    public string? UploaderType { get; set; }   // "user", "admin", "business_user"
    public string? AltText { get; set; }
    public string? Caption { get; set; }
    public string? ContentType { get; set; }   // e.g. "image/webp"
    public string? Checksum { get; set; }      // sha256 hex
    public required string Tag { get; set; } = "Generic";
}
