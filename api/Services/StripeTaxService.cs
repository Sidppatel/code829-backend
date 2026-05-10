using Serilog;
using Stripe;
using Stripe.Tax;

namespace Api.Services;

public class StripeTaxService(ISecretsProvider secrets) : ITaxService
{
    public async Task<TaxCalculationResult> CalculateAsync(
        int amountCents,
        string currency,
        string line1,
        string city,
        string state,
        string postalCode,
        string country = "US",
        CancellationToken ct = default)
    {
        var client = await GetClientAsync();
        var service = new CalculationService(client);

        var options = new CalculationCreateOptions
        {
            Currency = currency,
            LineItems =
            [
                new CalculationLineItemOptions
                {
                    Amount = amountCents,
                    Reference = "tickets"
                }
            ],
            CustomerDetails = new CalculationCustomerDetailsOptions
            {
                Address = new AddressOptions
                {
                    Line1 = line1,
                    City = city,
                    State = state,
                    PostalCode = postalCode,
                    Country = country
                },
                AddressSource = "billing"
            }
        };

        try
        {
            var calculation = await service.CreateAsync(options, cancellationToken: ct);

            Log.Information(
                "[StripeTax] Calculated tax for {Amount} cents at {Location}: total={Total}, tax={Tax}",
                amountCents, $"{city}, {state} {postalCode}", calculation.AmountTotal, calculation.TaxAmountExclusive);

            return new TaxCalculationResult(
                calculation.Id,
                (int)calculation.AmountTotal,
                (int)calculation.TaxAmountExclusive);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[StripeTax] Tax calculation failed for {Amount} cents", amountCents);
            throw new InvalidOperationException($"Tax calculation failed: {ex.StripeError?.Message ?? ex.Message}", ex);
        }
    }

    public async Task<string> CreateTransactionAsync(string calculationId, string reference, CancellationToken ct = default)
    {
        var client = await GetClientAsync();
        var service = new TransactionService(client);

        var options = new TransactionCreateFromCalculationOptions
        {
            Calculation = calculationId,
            Reference = reference
        };

        try
        {
            var transaction = await service.CreateFromCalculationAsync(options, cancellationToken: ct);
            Log.Information("[StripeTax] Created tax transaction {TxnId} from calculation {CalcId}",
                transaction.Id, calculationId);
            return transaction.Id;
        }
        catch (StripeException ex)
        {

            var existingId = TryExtractAlreadyCreatedTransactionId(ex);
            if (existingId is not null)
            {
                Log.Information(
                    "[StripeTax] Tax transaction {TxnId} already created from calculation {CalcId} — reusing",
                    existingId, calculationId);
                return existingId;
            }

            Log.Error(ex, "[StripeTax] Failed to create tax transaction from calculation {CalcId}", calculationId);
            throw new InvalidOperationException($"Tax transaction creation failed: {ex.StripeError?.Message ?? ex.Message}", ex);
        }
    }

    private static string? TryExtractAlreadyCreatedTransactionId(StripeException ex)
    {
        var msg = ex.StripeError?.Message ?? ex.Message ?? string.Empty;
        if (!msg.Contains("already created tax transaction", StringComparison.OrdinalIgnoreCase)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(msg, @"\btax_[A-Za-z0-9]+\b");
        return match.Success ? match.Value : null;
    }

    public async Task CreateReversalAsync(string taxTransactionId, string reference, CancellationToken ct = default)
    {
        var client = await GetClientAsync();
        var service = new TransactionService(client);

        var options = new TransactionCreateReversalOptions
        {
            OriginalTransaction = taxTransactionId,
            Reference = reference,
            Mode = "full"
        };

        try
        {
            var reversal = await service.CreateReversalAsync(options, cancellationToken: ct);
            Log.Information("[StripeTax] Created tax reversal {RevId} for transaction {TxnId}",
                reversal.Id, taxTransactionId);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "[StripeTax] Failed to reverse tax transaction {TxnId}", taxTransactionId);
            throw new InvalidOperationException($"Tax reversal failed: {ex.StripeError?.Message ?? ex.Message}", ex);
        }
    }

    private Task<StripeClient> GetClientAsync()
    {
        var key = secrets.StripeSecretKey;
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("Stripe is not configured — set STRIPE_SECRET_KEY environment variable");

        return Task.FromResult(new StripeClient(key));
    }
}
