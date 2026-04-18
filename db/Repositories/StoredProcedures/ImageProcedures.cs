using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class ImageProcedures(EventPlatformDbContext context) : IImageProcedures
{
    public async Task<Guid> CreateImageAsync(string entityType, Guid entityId, string storageKey, string originalName, int sizeBytes, int width, int height, bool isPrimary, int sortOrder, Guid? uploadedBy = null, string? uploaderType = null, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_image(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10) AS \"Value\"",
                new NpgsqlParameter("p0", entityType),
                new NpgsqlParameter("p1", entityId),
                new NpgsqlParameter("p2", storageKey),
                new NpgsqlParameter("p3", originalName),
                new NpgsqlParameter("p4", sizeBytes),
                new NpgsqlParameter("p5", width),
                new NpgsqlParameter("p6", height),
                new NpgsqlParameter("p7", isPrimary),
                new NpgsqlParameter("p8", sortOrder),
                new NpgsqlParameter("p9", NpgsqlDbType.Uuid) { Value = (object?)uploadedBy ?? DBNull.Value },
                new NpgsqlParameter("p10", NpgsqlDbType.Varchar) { Value = (object?)uploaderType ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }

    public async Task<string> DeleteImageAsync(Guid imageId, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<string>(
                "SELECT sp_delete_image(@p0) AS \"Value\"",
                imageId)
            .FirstAsync(ct);

        return result;
    }
}
