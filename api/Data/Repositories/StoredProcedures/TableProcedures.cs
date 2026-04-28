using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class TableProcedures(EventPlatformDbContext context) : ITableProcedures
{
    public async Task<Guid> CreateEventTableAsync(
        Guid? eventId, string? label, int? capacity, string? shape,
        string? color, int? priceCents, int? platformFeeCents, Guid? templateId,
        int? rowSpan = null, int? colSpan = null)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_event_table(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9) AS \"Value\"",
                new NpgsqlParameter("p0", NpgsqlDbType.Uuid) { Value = (object?)eventId ?? DBNull.Value },
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)label ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Integer) { Value = (object?)capacity ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)shape ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)color ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Integer) { Value = (object?)priceCents ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Integer) { Value = (object?)platformFeeCents ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Uuid) { Value = (object?)templateId ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Integer) { Value = (object?)rowSpan ?? DBNull.Value },
                new NpgsqlParameter("p9", NpgsqlDbType.Integer) { Value = (object?)colSpan ?? DBNull.Value })
            .FirstAsync();

        return result;
    }

    public async Task<Guid> CreateTableAsync(
        Guid? eventTableId, Guid? eventId, string? label,
        int? gridRow, int? gridCol, int? sortOrder,
        int rowSpan = 1, int colSpan = 1)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_table(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7) AS \"Value\"",
                new NpgsqlParameter("p0", NpgsqlDbType.Uuid) { Value = (object?)eventTableId ?? DBNull.Value },
                new NpgsqlParameter("p1", NpgsqlDbType.Uuid) { Value = (object?)eventId ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Text) { Value = (object?)label ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Integer) { Value = (object?)gridRow ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Integer) { Value = (object?)gridCol ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Integer) { Value = (object?)sortOrder ?? DBNull.Value },
                new NpgsqlParameter("p6", rowSpan),
                new NpgsqlParameter("p7", colSpan))
            .FirstAsync();

        return result;
    }

    public async Task<TableLockResult> LockTableAsync(
        Guid? userId, Guid? eventId, Guid? tableId, int? holdMinutes)
    {
        var result = await context.Database
            .SqlQueryRaw<TableLockResult>(
                "SELECT * FROM sp_lock_table(@p0, @p1, @p2, @p3)",
                userId!, eventId!, tableId!, holdMinutes!)
            .FirstAsync();

        return result;
    }

    public async Task<bool> ReleaseTableLockAsync(Guid? userId, Guid? eventId, Guid? tableId)
    {
        var result = await context.Database
            .SqlQueryRaw<bool>(
                "SELECT sp_release_table_lock(@p0, @p1, @p2) AS \"Value\"",
                userId!, eventId!, tableId!)
            .FirstAsync();

        return result;
    }

    public async Task MarkTableBookedAsync(Guid? tableId)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_mark_table_booked(@p0)",
            tableId!);
    }

    public async Task<int> CleanupExpiredLocksAsync()
    {
        var result = await context.Database
            .SqlQueryRaw<int>("SELECT sp_cleanup_expired_locks() AS \"Value\"")
            .FirstAsync();

        return result;
    }
}
