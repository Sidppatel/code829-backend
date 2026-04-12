using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class SettingsProcedures(EventPlatformDbContext context) : ISettingsProcedures
{
    public async Task UpsertSettingAsync(string key, string encryptedValue, string? description = null, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_upsert_setting(@p0, @p1, @p2)",
                [key, encryptedValue, (object?)description ?? DBNull.Value], ct);
    }
}
