namespace Db.Repositories.StoredProcedures;

public interface IVenueProcedures
{
    Task<Guid> CreateVenueAsync(
        string? name, string? description, string? imagePath,
        string? phone, string? email, string? website,
        string? line1, string? line2, string? city, string? state, string? zip);

    Task UpdateVenueAsync(
        Guid id, string? name, string? description, string? imagePath,
        string? phone, string? email, string? website, bool? isActive,
        string? line1, string? city, string? state, string? zip);
}
