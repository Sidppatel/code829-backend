using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class PerformerProcedures(EventPlatformDbContext context) : IPerformerProcedures
{
    public async Task<Guid> CreatePerformerAsync(string name, string slug, string? imagePath, string metaJson, CancellationToken ct = default)
    {
        var metaParam = new NpgsqlParameter("p3", NpgsqlDbType.Jsonb) { Value = metaJson };
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_performer({0}, {1}, {2}, {3}) AS \"Value\"",
                name, slug, imagePath ?? string.Empty, metaParam)
            .FirstAsync(ct);
        return result;
    }

    public async Task UpdatePerformerAsync(Guid id, string? name, string? slug, string? imagePath, string? metaJson, CancellationToken ct = default)
    {
        var metaParam = new NpgsqlParameter("p4", NpgsqlDbType.Jsonb) { Value = (object?)metaJson ?? DBNull.Value };
        var parameters = new object[]
        {
            id,
            (object?)name ?? DBNull.Value,
            (object?)slug ?? DBNull.Value,
            (object?)imagePath ?? DBNull.Value,
            metaParam
        };
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_performer({0}, {1}, {2}, {3}, {4})",
            parameters,
            ct);
    }

    public async Task DeletePerformerAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_delete_performer({0})",
            new object[] { id },
            ct);
    }

    public async Task SetEventPerformersAsync(Guid eventId, string linksJson, CancellationToken ct = default)
    {
        var linksParam = new NpgsqlParameter("p1", NpgsqlDbType.Jsonb) { Value = linksJson };
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_set_event_performers({0}, {1})",
            new object[] { eventId, linksParam },
            ct);
    }
}
