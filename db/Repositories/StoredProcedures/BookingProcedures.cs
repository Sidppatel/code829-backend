using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class BookingProcedures(EventPlatformDbContext context) : IBookingProcedures
{
    public async Task<Guid> CreateBookingAsync(Guid userId, Guid eventId, Guid? tableId, int? seats, Guid? eventTicketTypeId, int subtotalCents, int feeCents, int totalCents, string bookingNumber, string status = "Pending", CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_booking(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9) AS \"Value\"",
                new NpgsqlParameter("p0", userId),
                new NpgsqlParameter("p1", eventId),
                new NpgsqlParameter("p2", NpgsqlDbType.Uuid) { Value = (object?)tableId ?? DBNull.Value },
                new NpgsqlParameter("p3", NpgsqlDbType.Integer) { Value = (object?)seats ?? DBNull.Value },
                new NpgsqlParameter("p4", NpgsqlDbType.Uuid) { Value = (object?)eventTicketTypeId ?? DBNull.Value },
                new NpgsqlParameter("p5", subtotalCents),
                new NpgsqlParameter("p6", feeCents),
                new NpgsqlParameter("p7", totalCents),
                new NpgsqlParameter("p8", bookingNumber),
                new NpgsqlParameter("p9", status))
            .FirstAsync(ct);

        return result;
    }

    public async Task ConfirmBookingAsync(Guid bookingId, string qrToken, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_confirm_booking(@p0, @p1)",
                [bookingId, qrToken], ct);
    }

    public async Task CancelBookingAsync(Guid bookingId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_cancel_booking(@p0)",
                [bookingId], ct);
    }

    public async Task RefundBookingAsync(Guid bookingId, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_refund_booking(@p0)",
                [bookingId], ct);
    }
}
