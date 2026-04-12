using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class PaymentProcedures(EventPlatformDbContext context) : IPaymentProcedures
{
    public async Task<Guid> CreatePaymentAsync(Guid bookingId, string intentId, int amountCents, string currency = "usd", CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_payment(@p0, @p1, @p2, @p3) AS \"Value\"",
                bookingId, intentId, amountCents, currency)
            .FirstAsync(ct);

        return result;
    }

    public async Task UpdatePaymentStatusAsync(string intentId, string status, CancellationToken ct = default)
    {
        await context.Database
            .ExecuteSqlRawAsync(
                "SELECT sp_update_payment_status(@p0, @p1)",
                [intentId, status], ct);
    }
}
