using NpgsqlTypes;

namespace Db.Entities.Views;

/// <summary>
/// Read-only view entity mapping to v_events.
/// Resolves COALESCE defaults from venue_layouts and event_templates.
/// </summary>
public class EventView
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public string Category { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public string LayoutMode { get; set; } = null!;
    public string? EditorMode { get; set; }
    public int? GridRows { get; set; }
    public int? GridCols { get; set; }
    public int? MaxCapacity { get; set; }
    public int? PlatformFeePercent { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public Guid VenueId { get; set; }
    public Guid OrganizerId { get; set; }
    public Guid? VenueLayoutId { get; set; }
    public Guid? EventTemplateId { get; set; }
    public NpgsqlTsVector? SearchVector { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Flattened venue columns
    public string VenueName { get; set; } = null!;
    public string VenueAddress { get; set; } = null!;
    public string VenueCity { get; set; } = null!;
    public string VenueState { get; set; } = null!;
    public string VenueZipCode { get; set; } = null!;
    public string? VenueImagePath { get; set; }
}
