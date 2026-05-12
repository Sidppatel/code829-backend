namespace Db.Entities;

public class Performer : BaseEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? PrimaryImagePath { get; set; }
    public string Meta { get; set; } = "[]";
}
