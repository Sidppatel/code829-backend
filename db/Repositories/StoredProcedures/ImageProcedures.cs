using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class ImageProcedures(EventPlatformDbContext context) : IImageProcedures
{
    public async Task<Guid> CreateImageAsync(string entityType, Guid entityId, string storageKey, string originalName, int sizeBytes, int width, int height, bool isPrimary, int sortOrder, Guid? uploadedBy = null, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_image(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9) AS \"Value\"",
                entityType, entityId, storageKey, originalName, sizeBytes,
                width, height, isPrimary, sortOrder, (object?)uploadedBy ?? DBNull.Value)
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
