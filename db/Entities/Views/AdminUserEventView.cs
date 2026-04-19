namespace Db.Entities.Views;

public class AdminUserEventView
{
    public Guid AdminUserEventId { get; set; }
    public Guid AdminUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool AdminUserIsActive { get; set; }

    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventSlug { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string EventStatus { get; set; } = string.Empty;

    public Guid? AssignedByAdminUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
