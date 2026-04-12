namespace Db.Repositories.StoredProcedures;

public interface IPaymentProcedures
{
    Task<Guid> CreatePaymentAsync(Guid bookingId, string intentId, int amountCents, string currency = "usd", CancellationToken ct = default);
    Task UpdatePaymentStatusAsync(string intentId, string status, CancellationToken ct = default);
}
