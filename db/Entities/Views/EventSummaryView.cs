namespace Db.Entities.Views;

/// <summary>
/// Read-only view entity mapping to v_event_summary.
/// Lightweight aggregated view for event listing pages.
/// </summary>
public class EventSummaryView
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Category { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public string VenueName { get; set; } = null!;
    public string VenueCity { get; set; } = null!;
    public string OrganizerName { get; set; } = null!;
    public int TicketTypeCount { get; set; }
    public long TotalCapacity { get; set; }
    public long TotalSold { get; set; }
}
