using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class LayoutProcedures(EventPlatformDbContext context) : ILayoutProcedures
{

    public async Task<Guid> CreateTableTemplateAsync(string name, int capacity, string shape, string? color, int priceCents, int defaultRowSpan = 1, int defaultColSpan = 1, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_table_template(@p0, @p1, @p2, @p3, @p4, @p5, @p6) AS \"Value\"",
                new NpgsqlParameter("p0", name),
                new NpgsqlParameter("p1", capacity),
                new NpgsqlParameter("p2", shape),
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)color ?? DBNull.Value },
                new NpgsqlParameter("p4", priceCents),
                new NpgsqlParameter("p5", defaultRowSpan),
                new NpgsqlParameter("p6", defaultColSpan))
            .FirstAsync(ct);
    }

    public async Task UpdateTableTemplateAsync(Guid id, string? name, int? capacity, string? shape, string? color, int? priceCents, bool? isActive, int? defaultRowSpan = null, int? defaultColSpan = null, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_table_template(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
            [
                new NpgsqlParameter("p0", id),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)name ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Integer) { Value = (object?)capacity ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)shape ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)color ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Integer) { Value = (object?)priceCents ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Boolean) { Value = (object?)isActive ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Integer) { Value = (object?)defaultRowSpan ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Integer) { Value = (object?)defaultColSpan ?? DBNull.Value }
            ], ct);
    }

    public async Task DeactivateTableTemplateAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync("SELECT sp_deactivate_table_template({0})", [id], ct);
    }

    public async Task<TableTemplate?> GetTableTemplateByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.TableTemplates
            .FromSqlRaw("SELECT * FROM sp_get_table_template_by_id({0})", id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<TableTemplate>> ListTableTemplatesAsync(CancellationToken ct = default)
    {
        return await context.TableTemplates
            .FromSqlRaw("SELECT * FROM sp_list_active_table_templates()")
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<TableTemplate>> ListActiveTableTemplatesByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idArray = ids.ToArray();
        return await context.TableTemplates
            .FromSqlRaw("SELECT * FROM sp_list_active_table_templates_by_ids({0})", idArray)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Event?> GetEventByIdForLayoutAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Events
            .FromSqlRaw("SELECT * FROM sp_get_event_by_id_for_layout({0})", id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateEventGridAsync(Guid id, int? gridRows, int? gridCols, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_event_grid(@p0, @p1, @p2)",
            [
                new NpgsqlParameter("p0", id),
                new NpgsqlParameter("p1", NpgsqlDbType.Integer) { Value = (object?)gridRows ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Integer) { Value = (object?)gridCols ?? DBNull.Value }
            ], ct);
    }

    public async Task UpdateEventTableAsync(Guid id, string? label, int? capacity, string? shape, string? color, int? priceCents, bool? isActive, int? platformFeeCents = null, int? rowSpan = null, int? colSpan = null, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_event_table(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9)",
            [
                new NpgsqlParameter("p0", id),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)label ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Integer) { Value = (object?)capacity ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Text) { Value = (object?)shape ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Text) { Value = (object?)color ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Integer) { Value = (object?)priceCents ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Boolean) { Value = (object?)isActive ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Integer) { Value = (object?)platformFeeCents ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Integer) { Value = (object?)rowSpan ?? DBNull.Value },
                new NpgsqlParameter("p9", NpgsqlDbType.Integer) { Value = (object?)colSpan ?? DBNull.Value }
            ], ct);
    }

    public async Task DeleteEventTableAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync("SELECT sp_delete_event_table({0})", [id], ct);
    }

    public async Task<EventTable?> GetEventTableByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.EventTables
            .FromSqlRaw("SELECT * FROM sp_get_event_table_by_id({0})", id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<EventTable>> ListEventTablesForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.EventTables
            .FromSqlRaw("SELECT * FROM sp_list_event_tables_for_event({0})", eventId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<HashSet<Guid>> ListExistingEventTableTemplateIdsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ids = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT * FROM sp_list_existing_event_table_template_ids({0})", eventId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task UpdateTableAsync(Guid id, string? label, Guid? eventTableId, int? gridRow, int? gridCol, bool? isActive, int? sortOrder, int? rowSpan = null, int? colSpan = null, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_update_table(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
            [
                new NpgsqlParameter("p0", id),
                new NpgsqlParameter("p1", NpgsqlDbType.Text) { Value = (object?)label ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Uuid) { Value = (object?)eventTableId ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Integer) { Value = (object?)gridRow ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Integer) { Value = (object?)gridCol ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Boolean) { Value = (object?)isActive ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Integer) { Value = (object?)sortOrder ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Integer) { Value = (object?)rowSpan ?? DBNull.Value },
                new NpgsqlParameter("p8", NpgsqlDbType.Integer) { Value = (object?)colSpan ?? DBNull.Value }
            ], ct);
    }

    public async Task DeleteTableAsync(Guid id, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync("SELECT sp_delete_table({0})", [id], ct);
    }

    public async Task<Table?> GetTableByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Tables
            .FromSqlRaw("SELECT * FROM sp_get_table_by_id({0})", id)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Table>> ListTablesForEventAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.Tables
            .FromSqlRaw("SELECT * FROM sp_list_tables_for_event({0})", eventId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> EventHasActivePurchasesAsync(Guid eventId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_event_has_active_purchases({0}) AS \"Value\"", eventId)
            .FirstAsync(ct);
    }

    public async Task<bool> EventTableHasActivePurchasesAsync(Guid eventId, Guid eventTableId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_event_table_has_active_purchases({0}, {1}) AS \"Value\"", eventId, eventTableId)
            .FirstAsync(ct);
    }

    public async Task<bool> EventTableHasLockedTablesAsync(Guid eventTableId, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<bool>("SELECT sp_event_table_has_locked_tables({0}) AS \"Value\"", eventTableId)
            .FirstAsync(ct);
    }

    public async Task<HashSet<Guid>> GetLockedTableIdsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ids = await context.Database
            .SqlQueryRaw<Guid>("SELECT * FROM sp_get_locked_table_ids({0})", eventId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task<List<GridOverlapPair>> CheckGridOverlapAsync(Guid eventId, Guid? skipTableId = null, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<GridOverlapPair>(
                "SELECT * FROM sp_check_grid_overlap(@p0, @p1)",
                new NpgsqlParameter("p0", eventId),
                new NpgsqlParameter("p1", NpgsqlDbType.Uuid) { Value = (object?)skipTableId ?? DBNull.Value })
            .ToListAsync(ct);
    }

    public async Task SaveEventLayoutAsync(Guid eventId, int? gridRows, int? gridCols, string tablesJson, Guid[] lockedIds, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync(
            "SELECT sp_save_event_layout(@p0, @p1, @p2, @p3::jsonb, @p4)",
            [
                new NpgsqlParameter("p0", eventId),
                new NpgsqlParameter("p1", NpgsqlDbType.Integer) { Value = (object?)gridRows ?? DBNull.Value },
                new NpgsqlParameter("p2", NpgsqlDbType.Integer) { Value = (object?)gridCols ?? DBNull.Value },
                new NpgsqlParameter("p3", tablesJson),
                new NpgsqlParameter("p4", lockedIds)
            ], ct);
    }
}
