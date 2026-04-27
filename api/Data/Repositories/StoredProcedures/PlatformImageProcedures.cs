using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class PlatformImageProcedures(EventPlatformDbContext context) : IPlatformImageProcedures
{
    public async Task<AddPlatformImageResult> AddAsync(
        string storageKey, string originalName,
        int sizeBytes, int width, int height,
        Guid? uploadedBy = null, string? tag = null, string? uploaderType = null,
        string? altText = null, string? caption = null,
        string? contentType = null, string? checksum = null,
        CancellationToken ct = default)
    {
        var row = await context.Database
            .SqlQueryRaw<AddPlatformImageRow>(
                "SELECT * FROM sp_add_platform_image(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11)",
                new NpgsqlParameter("p0", storageKey),
                new NpgsqlParameter("p1", originalName),
                new NpgsqlParameter("p2", sizeBytes),
                new NpgsqlParameter("p3", width),
                new NpgsqlParameter("p4", height),
                new NpgsqlParameter("p5", NpgsqlDbType.Uuid) { Value = (object?)uploadedBy ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)tag ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)uploaderType ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Text) { Value = (object?)altText ?? DBNull.Value },
                new NpgsqlParameter("p9", NpgsqlDbType.Text) { Value = (object?)caption ?? DBNull.Value },
                new NpgsqlParameter("p10", NpgsqlDbType.Text) { Value = (object?)contentType ?? DBNull.Value },
                new NpgsqlParameter("p11", NpgsqlDbType.Text) { Value = (object?)checksum ?? DBNull.Value })
            .FirstAsync(ct);

        return new AddPlatformImageResult(row.ImageId, row.PlatformImageId, row.SortOrder, row.IsPrimary);
    }

    public async Task<bool> RemoveAsync(Guid imageId, CancellationToken ct = default)
        => await context.Database
            .SqlQueryRaw<bool>("SELECT sp_remove_platform_image({0}) AS \"Value\"", imageId)
            .FirstAsync(ct);

    public async Task<bool> SetPrimaryAsync(Guid imageId, CancellationToken ct = default)
        => await context.Database
            .SqlQueryRaw<bool>("SELECT sp_set_platform_primary_image({0}) AS \"Value\"", imageId)
            .FirstAsync(ct);

    public async Task ReorderAsync(Guid[] imageIds, CancellationToken ct = default)
        => await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_reorder_platform_images(@p0)",
            [new NpgsqlParameter("p0", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = imageIds }],
            ct);

    public async Task<List<PlatformImageView>> ListAsync(string? tag = null, CancellationToken ct = default)
        => await context.PlatformImageViews
            .FromSqlRaw("SELECT * FROM sp_list_platform_images({0})", tag == null ? DBNull.Value : (object)tag)
            .AsNoTracking()
            .ToListAsync(ct);

    private sealed class AddPlatformImageRow
    {
        public Guid ImageId { get; set; }
        public Guid PlatformImageId { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }
}
