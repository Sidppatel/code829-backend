namespace Db.Entities.Views;

public class BusinessUserEventView
{
    public Guid BusinessUserEventId { get; set; }
    public Guid BusinessUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool BusinessUserIsActive { get; set; }

    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventSlug { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string EventStatus { get; set; } = string.Empty;

    public Guid? AssignedByBusinessUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
