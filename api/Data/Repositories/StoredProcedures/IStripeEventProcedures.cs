namespace Db.Repositories.StoredProcedures;

public interface IStripeEventProcedures
{
    Task<Guid> InsertTransferAsync(
    string stripeTransferId,
    string stripeAccountId,
    string? paymentIntentId,
    int amountCents,
    string? currency,
    string rawEventJson,
    CancellationToken ct = default);

    Task<Guid> UpsertPayoutAsync(
    string stripePayoutId,
    string stripeAccountId,
    int amountCents,
    string? currency,
    string status,
    DateTime? arrivalDate,
    DateTime? paidAt,
    string rawEventJson,
    CancellationToken ct = default);
}
