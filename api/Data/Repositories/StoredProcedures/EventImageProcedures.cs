using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class EventImageProcedures(EventPlatformDbContext context) : IEventImageProcedures
{
    public async Task<AddEventImageResult> AddAsync(
        Guid eventId, string storageKey, string originalName,
        int sizeBytes, int width, int height,
        Guid? uploadedBy = null, string? uploaderType = null,
        string? altText = null, string? caption = null,
        string? contentType = null, string? checksum = null,
        CancellationToken ct = default)
    {
        var row = await context.Database
            .SqlQueryRaw<AddEventImageRow>(
                "SELECT * FROM sp_add_event_image(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)",
                new NpgsqlParameter("p0", eventId),
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

        return new AddEventImageResult(row.ImageId, row.EventImageId, row.SortOrder, row.IsPrimary);
    }

    public async Task<bool> RemoveAsync(Guid eventId, Guid imageId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_remove_event_image({0}, {1}) AS \"Value\"",
                eventId, imageId)
            .FirstAsync(ct);
    }

    public async Task<bool> SetPrimaryAsync(Guid eventId, Guid imageId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_set_event_primary_image({0}, {1}) AS \"Value\"",
                eventId, imageId)
            .FirstAsync(ct);
    }

    public async Task ReorderAsync(Guid eventId, Guid[] imageIds, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_reorder_event_images(@p0, @p1)",
            [
                new NpgsqlParameter("p0", eventId),
                new NpgsqlParameter("p1", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = imageIds }
            ], ct);
    }

    public async Task<List<EventImageView>> ListAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.EventImageViews
            .FromSqlRaw("SELECT * FROM sp_list_event_images({0})", eventId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private sealed class AddEventImageRow
    {
        public Guid ImageId { get; set; }
        public Guid EventImageId { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }
}
