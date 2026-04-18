using Contracts.Enums;

namespace Db.Entities;

public class SystemLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogCategory Category { get; set; }
    public required string Action { get; set; }
    public string? Source { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? AdminUserId { get; set; }
    public string? CorrelationId { get; set; }
    public long? DurationMs { get; set; }
    public string? MetadataJson { get; set; }
}
