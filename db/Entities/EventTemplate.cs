using Contracts.Enums;

namespace Db.Entities;

/// <summary>
/// Reusable event blueprint. Events can reference a template to inherit default
/// category, layout mode, capacity, and fee settings. NULL overrides on the event fall back here.
/// </summary>
public class EventTemplate : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public EventCategory? Category { get; set; }
    public LayoutMode? LayoutMode { get; set; }
    public int? DefaultMaxCapacity { get; set; }
    public int? DefaultPlatformFeePercent { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Event> Events { get; set; } = [];
}
