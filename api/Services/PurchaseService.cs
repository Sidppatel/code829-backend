using System.Security.Cryptography;
using Api.Exceptions;
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
    IOrganizationProcedures organizationProc,
    IPaymentEnrichmentService paymentEnrichment
) : IPurchaseService
{
    public async Task<PurchaseDto> CreateAsync(Guid userId, CreatePurchaseRequest request)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == request.EventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != "Published")
            throw new InvalidOperationException("Event is not available for purchase");

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
            .Where(t => tableIds.Contains(t.TableId) && t.EventId == ev.EventId)
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
            new PricingQuoteRequest(ev.EventId, TableIds: tableIds));
        var subtotal = pricing.SubtotalCents;
        var fee = pricing.FeeCents;
        var total = pricing.TotalCents;
        var piAmount = pricing.PaymentIntentAmountCents;
        var taxCalculationId = pricing.TaxCalculationId;
        var estimatedTaxCents = pricing.TaxCents;
        var totalSeats = tables.Sum(t => t.Capacity);

        var organization = await organizationProc.GetByBusinessUserAsync(ev.BusinessUserId);
        await EnsurePayoutReadyIfEnforcedAsync(organization);

        var purchaseNumber = GeneratePurchaseNumber();
        var piMetadata = BuildPaymentIntentMetadata(
            purchaseNumber, ev, subtotal, fee, estimatedTaxCents, piAmount, taxCalculationId, tableCount: tables.Count);
        var piDescription = BuildPaymentIntentDescription(purchaseNumber, ev, tableCount: tables.Count);
        var piStatementSuffix = BuildStatementDescriptorSuffix(ev);

        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            piAmount, subtotal, organization?.StripeConnectedAccountId, "usd", piMetadata,
            description: piDescription, statementDescriptorSuffix: piStatementSuffix);

        var purchaseId = await purchaseProc.CreatePurchaseAsync(
            userId, ev.EventId, tables[0].TableId, totalSeats, null, subtotal, fee, total, purchaseNumber);

        foreach (var table in tables.Skip(1))
        {
            var tableId = table.TableId;
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO purchase_tables (\"PurchaseId\", \"TableId\") VALUES ({purchaseId}, {tableId}) ON CONFLICT DO NOTHING");
        }

        await stripeTransactionProc.CreateAsync(purchaseId, intentId, piAmount, subtotal, taxCalculationId);

        var tableLabels = string.Join(", ", tables.Select(t => t.Label));
        Log.Information("[Purchase] Created multi-table purchase {PurchaseNumber} for tables [{Tables}], event {EventId}, total ${Total}, tax ${Tax}",
            purchaseNumber, tableLabels, ev.EventId, total / 100.0, estimatedTaxCents / 100.0);

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

        var ticketTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == request.EventId && tt.IsActive)
            .ToListAsync();

        EventTicketTypeSummaryView? selectedType = null;

        if (ticketTypes.Count > 0)
        {
            if (!request.EventTicketTypeId.HasValue)
                throw new InvalidOperationException("This event requires a ticket type selection");

            selectedType = ticketTypes.FirstOrDefault(tt => tt.EventTicketTypeId == request.EventTicketTypeId.Value)
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
            new PricingQuoteRequest(ev.EventId, SeatCount: seatsRequested, EventTicketTypeId: request.EventTicketTypeId));
        var subtotal = pricing.SubtotalCents;
        var fee = pricing.FeeCents;
        var total = pricing.TotalCents;
        var piAmount = pricing.PaymentIntentAmountCents;
        var taxCalculationId = pricing.TaxCalculationId;
        var estimatedTaxCents = pricing.TaxCents;

        var organization = await organizationProc.GetByBusinessUserAsync(ev.BusinessUserId);
        await EnsurePayoutReadyIfEnforcedAsync(organization);

        var purchaseNumber = GeneratePurchaseNumber();
        var piMetadata = BuildPaymentIntentMetadata(
            purchaseNumber, ev, subtotal, fee, estimatedTaxCents, piAmount, taxCalculationId, seats: seatsRequested);
        var piDescription = BuildPaymentIntentDescription(purchaseNumber, ev, seats: seatsRequested);
        var piStatementSuffix = BuildStatementDescriptorSuffix(ev);

        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            piAmount, subtotal, organization?.StripeConnectedAccountId, "usd", piMetadata,
            description: piDescription, statementDescriptorSuffix: piStatementSuffix);

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
            .FirstOrDefaultAsync(b => b.PurchaseId == purchaseId)
            ?? throw new KeyNotFoundException("Purchase not found");

        if (purchase.UserId != userId)
            throw new UnauthorizedAccessException("Not your purchase");

        if (purchase.Status != "Pending")
        {
            if (purchase.Status == "Paid" || purchase.Status == "CheckedIn")
            {
                return (await GetByIdAsync(purchaseId))!;
            }
            throw new InvalidOperationException($"Cannot confirm purchase in {purchase.Status} status");
        }

        if (purchase.TableId.HasValue)
        {
            var table = await context.TableViews.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TableId == purchase.TableId.Value);
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

        await paymentEnrichment.EnrichAndRecordAsync(purchase.PaymentIntentId, purchase.TaxCalculationId);

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var checkinLink = $"{frontendUrl}/purchases/{purchaseId}/tickets";

        // Re-query the purchase view to ensure the enriched payment and tax details from paymentEnrichment are populated
        var finalPurchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PurchaseId == purchaseId) ?? purchase;

        try
        {
            await emailService.SendAsync(
                finalPurchase.UserEmail,
                $"Purchase Confirmed — {finalPurchase.EventTitle} | {appName}",
                EmailTemplates.PurchaseConfirmed(
                    appName,
                    finalPurchase.UserFirstName,
                    finalPurchase.PurchaseNumber,
                    finalPurchase.EventTitle,
                    finalPurchase.TicketCount,
                    finalPurchase.TotalCents,
                    finalPurchase.TaxAmountCents ?? 0,
                    finalPurchase.TotalChargedCents ?? finalPurchase.TotalCents,
                    checkinLink)
            );
        }
        catch (Exception emailEx)
        {
            Log.Warning(emailEx, "[Purchase] Confirmation email failed for {PurchaseNumber} — purchase still confirmed", finalPurchase.PurchaseNumber);
        }

        Log.Information(
            "[Audit] purchase_confirmed purchase={PurchaseNumber} user={UserId} amount={Amount} qr={QrToken}",
            purchase.PurchaseNumber, userId, intent.AmountReceived, qrToken);
        return (await GetByIdAsync(purchaseId))!;
    }

    public async Task<PurchaseDto> CancelAsync(Guid purchaseId, Guid userId)
    {
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PurchaseId == purchaseId)
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
            .FirstOrDefaultAsync(b => b.PurchaseId == purchaseId)
            ?? throw new KeyNotFoundException("Purchase not found");

        if (purchase.Status != "Paid")
            throw new InvalidOperationException($"Cannot refund purchase in {purchase.Status} status");

        if (purchase.PaymentIntentId is not null)
            await paymentService.RefundPaymentAsync(purchase.PaymentIntentId);

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
            .FirstOrDefaultAsync(x => x.PurchaseId == purchaseId);

        if (b is null) return null;

        var venueAddress = !string.IsNullOrEmpty(b.VenueAddress)
            ? $"{b.VenueAddress}, {b.VenueCity}, {b.VenueState}"
            : null;

        return new PurchaseDto(
            b.PurchaseId, b.PurchaseNumber, b.Status,
            b.UserId, $"{b.UserFirstName} {b.UserLastName}", b.EventId, b.EventTitle,
            b.EventStartDate, b.EventEndDate, b.EventCategory, b.EventImagePath,
            b.VenueName, venueAddress,
            null, b.TotalCents, b.QrToken,
            b.TableId, b.TableLabel, b.TableLabels, b.SeatsReserved,
            b.EventTicketTypeId, b.EventTicketTypeLabel,
            b.TicketCount,
            b.StripeTransactionId.HasValue ? new StripeTransactionDto(
                b.StripeTransactionId.Value, b.PaymentIntentId!, b.PaymentStatus!,
                b.PaymentAmountCents ?? 0, b.TotalChargedCents, b.TaxAmountCents,
                b.StripeFeesCents, null, b.PaidAt, b.RefundedAt
            ) : null,
            b.CreatedAt
        );
    }

    public async Task<byte[]> GetQrImageAsync(Guid purchaseId, Guid userId)
    {
        var purchase = await context.PurchaseViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.PurchaseId == purchaseId)
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

    private async Task EnsurePayoutReadyIfEnforcedAsync(Db.Entities.Organization? organization)
    {
        var enforced = await settings.GetBoolAsync(SettingsKeys.ConnectEnforcementEnabled);
        if (!enforced) return;

        if (organization is null)
            throw new OrganizationNotPayoutReadyException(
                "This event's organizer hasn't been assigned to an organization yet — contact support");

        if (string.IsNullOrEmpty(organization.StripeConnectedAccountId) || !organization.StripeChargesEnabled)
            throw new OrganizationNotPayoutReadyException(
                "Organizer not yet configured for payouts");
    }

    private static Dictionary<string, string> BuildPaymentIntentMetadata(
        string purchaseNumber,
        EventView ev,
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
            ["event_id"] = ev.EventId.ToString(),
            ["event_name"] = Truncate(ev.Title, 500),
            ["event_type"] = ev.LayoutMode,
            ["event_start_date"] = ev.StartDate.ToString("o"),
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

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    private static string BuildPaymentIntentDescription(
    string purchaseNumber, EventView ev, int? tableCount = null, int? seats = null) =>
    PaymentDescriptions.Build(purchaseNumber, ev.Title, tableCount, seats);

    private static string BuildStatementDescriptorSuffix(EventView ev)
    {
        var title = ev.Title ?? string.Empty;

        var ascii = new string(title
            .Where(c => c >= ' ' && c <= '~' && c is not ('<' or '>' or '"' or '\'' or '*'))
            .ToArray()).Trim();
        if (string.IsNullOrEmpty(ascii)) ascii = "Event";
        return ascii.Length > 22 ? ascii[..22] : ascii;
    }

    public static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
