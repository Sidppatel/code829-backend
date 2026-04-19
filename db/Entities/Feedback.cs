namespace Db.Entities;

public class Feedback : BaseEntity
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    public required string Type { get; set; }       // General, Bug, Suggestion, Compliment, Complaint
    public required string Message { get; set; }
    public int Rating { get; set; }                  // 1–5 stars, 0 = not rated
    public Guid? UserId { get; set; }                // null for anonymous
    public User? User { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? Diagnostics { get; set; }         // JSON blob: console log buffer, URL, app version
}
