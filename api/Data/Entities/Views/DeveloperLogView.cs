namespace Db.Entities.Views;

public class DeveloperLogView
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public int? StatusCode { get; set; }
    public Guid? BusinessUserId { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
}
