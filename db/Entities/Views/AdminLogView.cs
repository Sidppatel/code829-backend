namespace Db.Entities.Views;

public class AdminLogView
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? AdminUserId { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminRole { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Description { get; set; }
    public string? MetadataJson { get; set; }
    public string? IpAddress { get; set; }
}
