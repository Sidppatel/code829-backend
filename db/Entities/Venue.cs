namespace Db.Entities;

public class Venue : BaseEntity
{
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string ZipCode { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Event> Events { get; set; } = [];
}
