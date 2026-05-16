using Db.Repositories.StoredProcedures;
using Serilog;
using Stripe;

namespace Api.Services;

public class PaymentEnrichmentService(
    ISecretsProvider secrets,
    IStripeTransactionProcedures stripeTransactionProc,
    ITaxService taxService,
    IPaymentService paymentService
) : IPaymentEnrichmentService
{
    public async Task EnrichAndRecordAsync(string paymentIntentId, string? taxCalculationId)
    {
        await EnrichTransactionAsync(paymentIntentId, taxCalculationId);
        await RecordTaxTransactionAsync(paymentIntentId, taxCalculationId);
    }

    private async Task EnrichTransactionAsync(string paymentIntentId, string? taxCalculationId)
    {
        try
        {
            var stripeKey = secrets.StripeSecretKey;
            if (string.IsNullOrEmpty(stripeKey)) return;

            var client = new StripeClient(stripeKey);
            var piService = new PaymentIntentService(client);
            var expanded = await piService.GetAsync(paymentIntentId, new PaymentIntentGetOptions
            {
                Expand = ["latest_charge.balance_transaction"]
            });

            var stripeFees = (int)(expanded.LatestCharge?.BalanceTransaction?.Fee ?? 0);
            var totalCharged = (int)expanded.AmountReceived;

            var taxAmount = 0;
            if (!string.IsNullOrEmpty(taxCalculationId))
            {
                var calcService = new Stripe.Tax.CalculationService(client);
                var calculation = await calcService.GetAsync(taxCalculationId);
                taxAmount = (int)calculation.TaxAmountExclusive;
            }

            await stripeTransactionProc.EnrichAsync(paymentIntentId, totalCharged, taxAmount, stripeFees);
            Log.Information(
                "[PaymentEnrichment] Enriched transaction {IntentId}: charged={Charged}, tax={Tax}, fees={Fees}",
                paymentIntentId, totalCharged, taxAmount, stripeFees);
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "[PaymentEnrichment] Failed to enrich transaction {IntentId} — non-critical",
                paymentIntentId);
        }
    }

    private async Task RecordTaxTransactionAsync(string paymentIntentId, string? taxCalculationId)
    {
        if (string.IsNullOrEmpty(taxCalculationId)) return;

        try
        {
            var taxTxnId = await taxService.CreateTransactionAsync(taxCalculationId, paymentIntentId);
            await stripeTransactionProc.SetTaxTransactionIdAsync(paymentIntentId, taxTxnId);
            await paymentService.UpdateMetadataAsync(paymentIntentId,
                new Dictionary<string, string> { ["tax_transaction"] = taxTxnId });
            Log.Information(
                "[PaymentEnrichment] Recorded tax transaction {TaxTxnId} for intent {IntentId}",
                taxTxnId, paymentIntentId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "[PaymentEnrichment] Failed to record tax transaction for intent {IntentId} — non-critical",
                paymentIntentId);
        }
    }
}
