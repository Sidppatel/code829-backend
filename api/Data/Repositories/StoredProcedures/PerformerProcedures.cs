using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class PerformerProcedures(EventPlatformDbContext context) : IPerformerProcedures
{
    public async Task<Guid> CreatePerformerAsync(string name, string slug, string? imagePath, string metaJson, CancellationToken ct = default)
    {
        var p0 = new NpgsqlParameter("p0", NpgsqlDbType.Text) { Value = name };
        var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = slug };
        var p2 = new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)imagePath ?? DBNull.Value };
        var p3 = new NpgsqlParameter("p3", NpgsqlDbType.Jsonb) { Value = metaJson };
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_performer({0}, {1}, {2}, {3}) AS \"Value\"",
                p0, p1, p2, p3)
            .FirstAsync(ct);
        return result;
    }

    public async Task UpdatePerformerAsync(Guid id, string? name, string? slug, string? imagePath, string? metaJson, CancellationToken ct = default)
    {
        var p0 = new NpgsqlParameter("p0", NpgsqlDbType.Uuid) { Value = id };
        var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)name ?? DBNull.Value };
        var p2 = new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)slug ?? DBNull.Value };
        var p3 = new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)imagePath ?? DBNull.Value };
        var p4 = new NpgsqlParameter("p4", NpgsqlDbType.Jsonb) { Value = (object?)metaJson ?? DBNull.Value };
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_performer({0}, {1}, {2}, {3}, {4})",
            new object[] { p0, p1, p2, p3, p4 },
            ct);
    }

    public async Task DeletePerformerAsync(Guid id, CancellationToken ct = default)
    {
        var p0 = new NpgsqlParameter("p0", NpgsqlDbType.Uuid) { Value = id };
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_delete_performer({0})",
            new object[] { p0 },
            ct);
    }

    public async Task SetEventPerformersAsync(Guid eventId, string linksJson, CancellationToken ct = default)
    {
        var p0 = new NpgsqlParameter("p0", NpgsqlDbType.Uuid) { Value = eventId };
        var p1 = new NpgsqlParameter("p1", NpgsqlDbType.Jsonb) { Value = linksJson };
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_set_event_performers({0}, {1})",
            new object[] { p0, p1 },
            ct);
    }
}
