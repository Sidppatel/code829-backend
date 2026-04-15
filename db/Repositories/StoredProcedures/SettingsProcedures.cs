using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class SettingsProcedures(EventPlatformDbContext context) : ISettingsProcedures
{
    public async Task UpsertSettingAsync(string key, string encryptedValue, string? description = null, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_upsert_setting(@p0, @p1, @p2)",
                [
                    new NpgsqlParameter("p0", key),
                    new NpgsqlParameter("p1", encryptedValue),
                    new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)description ?? DBNull.Value }
                ], ct);
    }
}
