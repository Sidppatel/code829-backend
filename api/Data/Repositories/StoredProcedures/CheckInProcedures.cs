using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class CheckInProcedures(EventPlatformDbContext context) : ICheckInProcedures
{
    public async Task<CheckInScanResult?> ScanTicketAsync(string qrToken, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<CheckInScanResult>(
                "SELECT * FROM sp_check_in_ticket(@p0)",
                qrToken)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async Task<CheckInScanResult?> ScanPurchaseAsync(string qrToken, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<CheckInScanResult>(
                "SELECT * FROM sp_check_in_purchase(@p0)",
                qrToken)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async Task<DateTime?> GetEventLastCheckinAsync(Guid eventId, CancellationToken ct = default)
    {
        var results = await context.Database
            .SqlQueryRaw<DateTime?>(
                "SELECT sp_get_event_last_checkin(@p0) AS \"Value\"",
                eventId)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }
}
