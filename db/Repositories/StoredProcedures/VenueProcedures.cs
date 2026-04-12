using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class VenueProcedures(EventPlatformDbContext context) : IVenueProcedures
{
    public async Task<Guid> CreateVenueAsync(
        string? name, string? description, string? imagePath,
        string? phone, string? email, string? website,
        string? line1, string? line2, string? city, string? state, string? zip)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_venue(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10) AS \"Value\"",
                name!, description!, imagePath!,
                phone!, email!, website!,
                line1!, line2!, city!, state!, zip!)
            .FirstAsync();

        return result;
    }

    public async Task UpdateVenueAsync(
        Guid id, string? name, string? description, string? imagePath,
        string? phone, string? email, string? website, bool? isActive,
        string? line1, string? city, string? state, string? zip)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_venue(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)",
            id, name!, description!, imagePath!,
            phone!, email!, website!, isActive!,
            line1!, city!, state!, zip!);
    }
}
