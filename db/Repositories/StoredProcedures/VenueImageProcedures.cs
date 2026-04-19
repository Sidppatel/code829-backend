using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class VenueImageProcedures(EventPlatformDbContext context) : IVenueImageProcedures
{
    public async Task<AddVenueImageResult> AddAsync(
        Guid venueId, string storageKey, string originalName,
        int sizeBytes, int width, int height,
        Guid? uploadedBy = null, string? uploaderType = null,
        string? altText = null, string? caption = null,
        string? contentType = null, string? checksum = null,
        CancellationToken ct = default)
    {
        var row = await context.Database
            .SqlQueryRaw<AddVenueImageRow>(
                "SELECT * FROM sp_add_venue_image(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)",
                new NpgsqlParameter("p0", venueId),
                new NpgsqlParameter("p1", storageKey),
                new NpgsqlParameter("p2", originalName),
                new NpgsqlParameter("p3", sizeBytes),
                new NpgsqlParameter("p4", width),
                new NpgsqlParameter("p5", height),
                new NpgsqlParameter("p6", NpgsqlDbType.Uuid) { Value = (object?)uploadedBy ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)uploaderType ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Text) { Value = (object?)altText ?? DBNull.Value },
                new NpgsqlParameter("p9", NpgsqlDbType.Text) { Value = (object?)caption ?? DBNull.Value },
                new NpgsqlParameter("p10", NpgsqlDbType.Text) { Value = (object?)contentType ?? DBNull.Value },
                new NpgsqlParameter("p11", NpgsqlDbType.Text) { Value = (object?)checksum ?? DBNull.Value })
            .FirstAsync(ct);

        return new AddVenueImageResult(row.ImageId, row.VenueImageId, row.SortOrder, row.IsPrimary);
    }

    public async Task<bool> RemoveAsync(Guid venueId, Guid imageId, CancellationToken ct = default)
        => await context.Database
            .SqlQueryRaw<bool>("SELECT sp_remove_venue_image({0}, {1}) AS \"Value\"", venueId, imageId)
            .FirstAsync(ct);

    public async Task<bool> SetPrimaryAsync(Guid venueId, Guid imageId, CancellationToken ct = default)
        => await context.Database
            .SqlQueryRaw<bool>("SELECT sp_set_venue_primary_image({0}, {1}) AS \"Value\"", venueId, imageId)
            .FirstAsync(ct);

    public async Task ReorderAsync(Guid venueId, Guid[] imageIds, CancellationToken ct = default)
        => await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_reorder_venue_images(@p0, @p1)",
            [
                new NpgsqlParameter("p0", venueId),
                new NpgsqlParameter("p1", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = imageIds }
            ], ct);

    public async Task<List<VenueImageView>> ListAsync(Guid venueId, CancellationToken ct = default)
        => await context.VenueImageViews
            .FromSqlRaw("SELECT * FROM sp_list_venue_images({0})", venueId)
            .AsNoTracking()
            .ToListAsync(ct);

    private sealed class AddVenueImageRow
    {
        public Guid ImageId { get; set; }
        public Guid VenueImageId { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }
}
