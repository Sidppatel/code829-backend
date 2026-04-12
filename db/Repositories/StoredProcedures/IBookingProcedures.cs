namespace Db.Repositories.StoredProcedures;

public interface IBookingProcedures
{
    Task<Guid> CreateBookingAsync(Guid userId, Guid eventId, Guid? tableId, int? seats, int subtotalCents, int feeCents, int totalCents, string bookingNumber, string status = "Pending", CancellationToken ct = default);
    Task ConfirmBookingAsync(Guid bookingId, string qrToken, CancellationToken ct = default);
    Task CancelBookingAsync(Guid bookingId, CancellationToken ct = default);
    Task RefundBookingAsync(Guid bookingId, CancellationToken ct = default);
}
