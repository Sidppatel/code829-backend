namespace Db.Entities.Views;

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
    public string LayoutMode { get; set; } = null!;
    public string VenueName { get; set; } = null!;
    public string VenueCity { get; set; } = null!;
    public string OrganizerName { get; set; } = null!;
    public long TotalCapacity { get; set; }
    public long TotalSold { get; set; }
}
