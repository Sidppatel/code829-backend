using System.Security.Cryptography;
using Contracts.DTOs.Purchases;
using Contracts.Enums;
using Db;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;

namespace Api.Services;

public class PurchaseService(
    EventPlatformDbContext context,
    IPurchaseProcedures purchaseProc,
    IStripeTransactionProcedures stripeTransactionProc,
    IPaymentService paymentService,
    ITaxService taxService,
    IPricingService pricingService,
    IEmailService emailService,
    ISettingsService settings,
    IAdminUserProcedures adminProc
) : IPurchaseService
{
    public async Task<PurchaseDto> CreateAsync(Guid userId, CreatePurchaseRequest request)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != "Published")
            throw new InvalidOperationException("Event is not available for purchase");

        // Normalize: TableIds takes precedence, fall back to single TableId
        var tableIds = request.TableIds is { Count: > 0 }
            ? request.TableIds
            : request.TableId.HasValue ? [request.TableId.Value] : null;

        if (tableIds is { Count: > 0 })
            return await CreateTablePurchaseAsync(userId, tableIds, ev);

        if (request.SeatsReserved.HasValue)
            return await CreateCapacityPurchaseAsync(userId, request, ev);

        throw new InvalidOperationException("Either TableId/TableIds (for Grid events) or SeatsReserved (for Open events) is required");
    }

    private async Task<PurchaseDto> CreateTablePurchaseAsync(Guid userId, List<Guid> tableIds, EventView ev)
    {
        if (ev.LayoutMode != "Grid")
            throw new InvalidOperationException("Table purchases are only available for Grid events");

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => tableIds.Contains(t.Id) && t.EventId == ev.Id)
            .ToListAsync();

        if (tables.Count != tableIds.Count)
            throw new KeyNotFoundException("One or more tables not found for this event");

        foreach (var table in tables)
        {
            if (table.Status != "Locked")
                throw new InvalidOperationException($"Table {table.Label} must be locked before purchase");
            if (table.LockedByUserId != userId)
                throw new InvalidOperationException($"You do not hold table {table.Label}");
            if (table.LockExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException($"Lock on table {table.Label} has expired");
        }

        var pricing = await pricingService.ComputeForPurchaseAsync(
            new PricingQuoteRequest(ev.Id, TableIds: tableIds));
        var subtotal = pricing.SubtotalCents;
        var fee = pricing.FeeCents;
        var total = pricing.TotalCents;
        var piAmount = pricing.PaymentIntentAmountCents;
        var taxCalculationId = pricing.TaxCalculationId;
        var estimatedTaxCents = pricing.TaxCents;
        var totalSeats = tables.Sum(t => t.Capacity);

        var organizer = await adminProc.GetByIdAsync(ev.OrganizerId);

        // Generate purchase number up-front so we can attach it to the PaymentIntent metadata.
        var purchaseNumber = GeneratePurchaseNumber();
        var piMetadata = BuildPaymentIntentMetadata(
            purchaseNumber, ev.Id, subtotal, fee, estimatedTaxCents, piAmount, taxCalculationId, tableCount: tables.Count);

        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            piAmount, subtotal, organizer?.StripeConnectedAccountId, "usd", piMetadata);

        // Create purchase with the first table as primary (for backward compat)
        var purchaseId = await purchaseProc.CreatePurchaseAsync(
            userId, ev.Id, tables[0].Id, totalSeats, null, subtotal, fee, total, purchaseNumber);

        // Insert additional tables into the purchase_tables junction table. Using
        // ExecuteSqlInterpolated (FormattableString) instead of ExecuteSqlRawAsync
        // so EF Core parameterizes the Guids at compile time — any future edit that
        // accidentally interpolates user input still goes through parameter binding.
        // The junction has no EF entity mapping; sp_create_purchase inserts the
        // primary table row and this loop adds the rest. ON CONFLICT DO NOTHING
        // keeps the call idempotent if the SP ever adds overlapping rows.
        foreach (var table in tables.Skip(1))
        {
            var tableId = table.Id;
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO purchase_tables (\"PurchaseId\", \"TableId\") VALUES ({purchaseId}, {tableId}) ON CONFLICT DO NOTHING");
        }

        await stripeTransactionProc.CreateAsync(purchaseId, intentId, piAmount, subtotal, taxCalculationId);

        var tableLabels = string.Join(", ", tables.Select(t => t.Label));
        Log.Information("[Purchase] Created multi-table purchase {PurchaseNumber} for tables [{Tables}], event {EventId}, total ${Total}, tax ${Tax}",
            purchaseNumber, tableLabels, ev.Id, total / 100.0, estimatedTaxCents / 100.0);

        var dto = await GetByIdAsync(purchaseId) ?? throw new InvalidOperationException("Purchase creation failed");

        if (estimatedTaxCents > 0 && dto.Transaction is not null)
        {
            dto = dto with
            {
                Transaction = dto.Transaction with
                {
                    TaxAmountCents = estimatedTaxCents,
                    TotalChargedCents = piAmount
                }
            };
        }

        return dto with { ClientSecret = clientSecret };
    }

    private async Task<PurchaseDto> CreateCapacityPurchaseAsync(Guid userId, CreatePurchaseRequest request, EventView ev)
    {
        if (ev.LayoutMode != "Open")
            throw new InvalidOperationException("Capacity reservations are only available for Open events");

        if (!ev.MaxCapacity.HasValue || ev.MaxCapacity <= 0)
            throw new InvalidOperationException("Event has no capacity configured");

        var seatsRequested = request.SeatsReserved!.Value;

        // Check if event has ticket types
        var ticketTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == request.EventId && tt.IsActive)
            .ToListAsync();

        EventTicketTypeSummaryView? selectedType = null;

        if (ticketTypes.Count > 0)
        {
            if (!request.EventTicketTypeId.HasValue)
                throw new InvalidOperationException("This event requires a ticket type selection");

            selectedType = ticketTypes.FirstOrDefault(tt => tt.Id == request.EventTicketTypeId.Value)
                ?? throw new KeyNotFoundException("Ticket type not found or inactive");

            if (selectedType.MaxQuantity.HasValue &&
                selectedType.SoldCount + seatsRequested > selectedType.MaxQuantity.Value)
                throw new InvalidOperationException(
                    $"Not enough availability for {selectedType.Label}. Available: {selectedType.AvailableCount}");
        }
        else
        {
            if (!ev.PricePerPersonCents.HasValue)
                throw new InvalidOperationException("Event has no price configured");
        }

        var pricing = await pricingService.ComputeForPurchaseAsync(
            new PricingQuoteRequest(ev.Id, SeatCount: seatsRequested, EventTicketTypeId: request.EventTicketTypeId));
        var subtotal = pricing.SubtotalCents;
        var fee = pricing.FeeCents;
        var total = pricing.TotalCents;
        var piAmount = pricing.PaymentIntentAmountCents;
        var taxCalculationId = pricing.TaxCalculationId;
        var estimatedTaxCents = pricing.TaxCents;

        var organizer = await adminProc.GetByIdAsync(ev.OrganizerId);

        var purchaseNumber = GeneratePurchaseNumber();
        var piMetadata = BuildPaymentIntentMetadata(
            purchaseNumber, ev.Id, subtotal, fee, estimatedTaxCents, piAmount, taxCalculationId, seats: seatsRequested);

        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            piAmount, subtotal, organizer?.StripeConnectedAccountId, "usd", piMetadata);

        // sp_reserve_open_capacity serializes capacity + ticket-type quota checks via row-level
        // locks on events/event_ticket_types rows. Replaces the previous Redis-lock + SELECT +
        // INSERT pattern which could race under concurrent load.
        Guid purchaseId;
        try
        {
            purchaseId = await purchaseProc.ReserveOpenCapacityAsync(
                userId, request.EventId, seatsRequested, request.EventTicketTypeId,
                subtotal, fee, total, purchaseNumber);
        }
        catch (Exception ex) when (ex.Message.Contains("capacity") || ex.Message.Contains("availability"))
        {
            Log.Warning(
                "[Audit] capacity_race_rejected event={EventId} user={UserId} requested={Seats} reason={Reason}",
                request.EventId, userId, seatsRequested, ex.Message);
            // Roll back the Stripe intent we just created so we don't orphan it
            try { await paymentService.RefundPaymentAsync(intentId); } catch { }
            throw new InvalidOperationException(ex.Message, ex);
        }

        await stripeTransactionProc.CreateAsync(purchaseId, intentId, piAmount, subtotal, taxCalculationId);

        Log.Information("[Purchase] Created capacity purchase {PurchaseNumber} for {Seats} seats, event {EventId}, total ${Total}, tax ${Tax}",
            purchaseNumber, seatsRequested, request.EventId, total / 100.0, estimatedTaxCents / 100.0);

        var dto = await GetByIdAsync(purchaseId) ?? throw new InvalidOperationException("Purchase creation failed");

        if (estimatedTaxCents > 0 && dto.Transaction is not null)
        {
            dto = dto with
            {
                Transaction = dto.Transaction with
                {
                    TaxAmountCents = estimatedTaxCents,
                    TotalChargedCents = piAmount
                }
            };
        }

        return dto with { ClientSecret = clientSecret };
    }

    public async Task<PurchaseDto> ConfirmPaymentAsync(Guid purchaseId, Guid userId)
    {
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == purchaseId)
            ?? throw new KeyNotFoundException("Purchase not found");

        if (purchase.UserId != userId)
            throw new UnauthorizedAccessException("Not your purchase");

        if (purchase.Status != "Pending")
            throw new InvalidOperationException($"Cannot confirm purchase in {purchase.Status} status");

        if (purchase.TableId.HasValue)
        {
            var table = await context.TableViews.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == purchase.TableId.Value);
            if (table is null || table.Status != "Locked" || table.LockedByUserId != userId)
                throw new InvalidOperationException("Table lock has expired. Please select a new table.");
        }

        if (purchase.PaymentIntentId is null)
            throw new InvalidOperationException("No payment associated with this purchase");

        var intent = await paymentService.GetPaymentIntentAsync(purchase.PaymentIntentId);
        if (intent.Status != "succeeded")
            throw new InvalidOperationException($"Payment has not succeeded (status: {intent.Status}). Please complete payment before confirming.");

        var expectedAmount = purchase.PaymentAmountCents ?? purchase.TotalCents;
        if (intent.AmountReceived != expectedAmount)
        {
            Log.Error(
                "[Audit] payment_amount_mismatch purchase={PurchaseNumber} intent={IntentId} expected={Expected} received={Received} user={UserId}",
                purchase.PurchaseNumber, purchase.PaymentIntentId, expectedAmount, intent.AmountReceived, userId);
            throw new InvalidOperationException("Payment amount does not match purchase total");
        }

        await stripeTransactionProc.UpdateStatusAsync(purchase.PaymentIntentId, "Succeeded");

        var qrToken = GenerateQrToken();
        await purchaseProc.ConfirmPurchaseAsync(purchaseId, qrToken);

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var checkinLink = $"{frontendUrl}/purchase/{purchaseId}/checkin";
        // Email is a notification, not a purchase invariant — a failure here (bad Resend domain,
        // network, etc.) should not un-confirm a paid purchase. Log and continue.
        try
        {
            await emailService.SendAsync(
                purchase.UserEmail,
                $"Purchase Confirmed — {purchase.EventTitle} | {appName}",
                EmailTemplates.PurchaseConfirmed(
                    appName, purchase.UserFirstName, purchase.PurchaseNumber,
                    purchase.EventTitle, $"${purchase.TotalCents / 100.0:F2}", checkinLink)
            );
        }
        catch (Exception emailEx)
        {
            Log.Warning(emailEx, "[Purchase] Confirmation email failed for {PurchaseNumber} — purchase still confirmed", purchase.PurchaseNumber);
        }

        Log.Information(
            "[Audit] purchase_confirmed purchase={PurchaseNumber} user={UserId} amount={Amount} qr={QrToken}",
            purchase.PurchaseNumber, userId, intent.AmountReceived, qrToken);
        return (await GetByIdAsync(purchaseId))!;
    }

    public async Task<PurchaseDto> CancelAsync(Guid purchaseId, Guid userId)
    {
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == purchaseId)
            ?? throw new KeyNotFoundException("Purchase not found");

        if (purchase.UserId != userId)
            throw new UnauthorizedAccessException("Not your purchase");

        if (purchase.Status is not ("Pending" or "Paid"))
            throw new InvalidOperationException($"Cannot cancel purchase in {purchase.Status} status");

        await purchaseProc.CancelPurchaseAsync(purchaseId);

        return (await GetByIdAsync(purchaseId))!;
    }

    public async Task<PurchaseDto> RefundAsync(Guid purchaseId)
    {
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == purchaseId)
            ?? throw new KeyNotFoundException("Purchase not found");

        if (purchase.Status != "Paid")
            throw new InvalidOperationException($"Cannot refund purchase in {purchase.Status} status");

        if (purchase.PaymentIntentId is not null)
            await paymentService.RefundPaymentAsync(purchase.PaymentIntentId);

        // Reverse the tax transaction if one was recorded. This is accounting-critical —
        // a missing reversal means we'll over-report sales tax remitted. Log as Error (not
        // Warning) with a TAX_REVERSAL_FAILED marker so alerts can pattern-match it.
        if (!string.IsNullOrEmpty(purchase.TaxTransactionId) && purchase.PaymentIntentId is not null)
        {
            try
            {
                await taxService.CreateReversalAsync(purchase.TaxTransactionId, $"{purchase.PaymentIntentId}-refund");
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "[Purchase] TAX_REVERSAL_FAILED purchase={PurchaseNumber} txTxn={TaxTxnId} intent={IntentId} — refund completed but tax not reversed; manual reconciliation required",
                    purchase.PurchaseNumber, purchase.TaxTransactionId, purchase.PaymentIntentId);
            }
        }

        await purchaseProc.RefundPurchaseAsync(purchaseId);

        return (await GetByIdAsync(purchaseId))!;
    }

    public async Task<PurchaseDto?> GetByIdAsync(Guid purchaseId)
    {
        var b = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == purchaseId);

        if (b is null) return null;

        var venueAddress = !string.IsNullOrEmpty(b.VenueAddress)
            ? $"{b.VenueAddress}, {b.VenueCity}, {b.VenueState}"
            : null;

        return new PurchaseDto(
            b.Id, b.PurchaseNumber, b.Status,
            b.UserId, $"{b.UserFirstName} {b.UserLastName}", b.EventId, b.EventTitle,
            b.EventStartDate, b.EventEndDate, b.EventCategory, b.EventImagePath,
            b.VenueName, venueAddress,
            b.SubtotalCents, b.TotalCents, b.QrToken,
            b.TableId, b.TableLabel, b.SeatsReserved,
            b.EventTicketTypeId, b.EventTicketTypeLabel,
            b.TicketCount,
            b.StripeTransactionId.HasValue ? new StripeTransactionDto(
                b.StripeTransactionId.Value, b.PaymentIntentId!, b.PaymentStatus!,
                b.PaymentAmountCents ?? 0, b.TotalChargedCents, b.TaxAmountCents,
                b.StripeFeesCents, b.TransferAmountCents, b.PaidAt, b.RefundedAt
            ) : null,
            b.CreatedAt
        );
    }

    public async Task<byte[]> GetQrImageAsync(Guid purchaseId, Guid userId)
    {
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == purchaseId)
            ?? throw new KeyNotFoundException("Purchase not found");

        if (purchase.UserId != userId)
            throw new UnauthorizedAccessException("Not your purchase");

        if (string.IsNullOrEmpty(purchase.QrToken))
            throw new InvalidOperationException("No QR token — purchase not yet confirmed");

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(purchase.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(10);
    }

    private static string GeneratePurchaseNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMdd");
        var random = RandomNumberGenerator.GetInt32(100000, 999999);
        return $"BK-{timestamp}-{random}";
    }

    // Builds PaymentIntent metadata so the payment, purchase, and tax breakdown are reconcilable
    // from the Stripe dashboard alone. Key "tax_calculation" is Stripe's standard for linking a
    // Tax Calculation to a PaymentIntent (see https://docs.stripe.com/tax/custom).
    // Payout split: admin_payout_cents goes to the organizer via transfer_data.amount;
    // developer_gross_cents = platform_fee + tax; developer owes the tax to the jurisdiction,
    // so developer's net revenue = platform_fee - stripe_fee (stripe_fee isn't known at create time).
    private static Dictionary<string, string> BuildPaymentIntentMetadata(
        string purchaseNumber,
        Guid eventId,
        int subtotalCents,
        int platformFeeCents,
        int taxCents,
        int totalCents,
        string? taxCalculationId,
        int? tableCount = null,
        int? seats = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["purchase_number"] = purchaseNumber,
            ["event_id"] = eventId.ToString(),
            ["subtotal_cents"] = subtotalCents.ToString(),
            ["platform_fee_cents"] = platformFeeCents.ToString(),
            ["tax_cents"] = taxCents.ToString(),
            ["total_cents"] = totalCents.ToString(),
            ["admin_payout_cents"] = subtotalCents.ToString(),
            ["developer_gross_cents"] = (platformFeeCents + taxCents).ToString()
        };
        if (!string.IsNullOrEmpty(taxCalculationId))
            metadata["tax_calculation"] = taxCalculationId;
        if (tableCount is int tc)
            metadata["table_count"] = tc.ToString();
        if (seats is int s)
            metadata["seats"] = s.ToString();
        return metadata;
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
