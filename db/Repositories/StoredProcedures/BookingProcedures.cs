using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class BookingProcedures(EventPlatformDbContext context) : IBookingProcedures
{
    public async Task<Guid> CreateBookingAsync(Guid userId, Guid eventId, Guid? tableId, int? seats, Guid? eventTicketTypeId, int subtotalCents, int feeCents, int totalCents, string bookingNumber, string status = "Pending", CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_booking(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9) AS \"Value\"",
                userId, eventId, (object?)tableId ?? DBNull.Value, (object?)seats ?? DBNull.Value,
                (object?)eventTicketTypeId ?? DBNull.Value,
                subtotalCents, feeCents, totalCents, bookingNumber, status)
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
