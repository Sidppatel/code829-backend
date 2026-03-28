using NpgsqlTypes;
using Contracts.Enums;

namespace Db.Entities;

public class Event : BaseEntity
{
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public EventCategory Category { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public LayoutMode LayoutMode { get; set; } = LayoutMode.None;
    public int? MaxCapacity { get; set; }
    public int? PlatformFeePercent { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// PostgreSQL tsvector column for full-text search.
    /// Auto-populated via a database trigger on title + description.
    /// </summary>
    public NpgsqlTsVector? SearchVector { get; set; }

    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;

    public Guid OrganizerId { get; set; }
    public User Organizer { get; set; } = null!;

    public ICollection<TicketType> TicketTypes { get; set; } = [];
}
