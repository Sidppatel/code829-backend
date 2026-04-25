using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class DashboardProcedures(EventPlatformDbContext context) : IDashboardProcedures
{
    public async Task<NextEventDashboardRow?> GetNextEventDashboardAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var nowParam = new NpgsqlParameter("p0", NpgsqlDbType.TimestampTz) { Value = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc) };
        var rows = await context.Database
            .SqlQueryRaw<NextEventDashboardRow>(
                "SELECT * FROM sp_get_next_event_dashboard(@p0)", nowParam)
            .ToListAsync(ct);
        return rows.FirstOrDefault();
    }

    public async Task<List<EventRecentPurchaseRow>> GetEventRecentPurchasesAsync(Guid eventId, int limit, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<EventRecentPurchaseRow>(
                "SELECT * FROM sp_get_event_recent_purchases(@p0, @p1)",
                new NpgsqlParameter("p0", eventId),
                new NpgsqlParameter("p1", limit))
            .ToListAsync(ct);
    }

    public async Task<MonthlyReportSummaryRow> GetMonthlyReportSummaryAsync(int year, int month, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<MonthlyReportSummaryRow>(
                "SELECT * FROM sp_get_monthly_report_summary(@p0, @p1)",
                new NpgsqlParameter("p0", year),
                new NpgsqlParameter("p1", month))
            .FirstAsync(ct);
    }

    public async Task<List<MonthlyReportByEventRow>> GetMonthlyReportByEventAsync(int year, int month, CancellationToken ct = default)
    {
        return await context.Database
            .SqlQueryRaw<MonthlyReportByEventRow>(
                "SELECT * FROM sp_get_monthly_report_by_event(@p0, @p1)",
                new NpgsqlParameter("p0", year),
                new NpgsqlParameter("p1", month))
            .ToListAsync(ct);
    }
}
