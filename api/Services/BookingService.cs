using System.Security.Cryptography;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;

namespace Api.Services;

public class BookingService(
    EventPlatformDbContext context,
    IBookingProcedures bookingProc,
    IStripeTransactionProcedures stripeTransactionProc,
    IPaymentService paymentService,
    ITaxService taxService,
    IPricingService pricingService,
    IEmailService emailService,
    ISettingsService settings
) : IBookingService
{
    public async Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != "Published")
            throw new InvalidOperationException("Event is not available for booking");

        // Normalize: TableIds takes precedence, fall back to single TableId
        var tableIds = request.TableIds is { Count: > 0 }
            ? request.TableIds
            : request.TableId.HasValue ? [request.TableId.Value] : null;

        if (tableIds is { Count: > 0 })
            return await CreateTableBookingAsync(userId, tableIds, ev);

        if (request.SeatsReserved.HasValue)
            return await CreateCapacityBookingAsync(userId, request, ev);

        throw new InvalidOperationException("Either TableId/TableIds (for Grid events) or SeatsReserved (for Open events) is required");
    }

    private async Task<BookingDto> CreateTableBookingAsync(Guid userId, List<Guid> tableIds, EventView ev)
    {
        if (ev.LayoutMode != "Grid")
            throw new InvalidOperationException("Table bookings are only available for Grid events");

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => tableIds.Contains(t.Id) && t.EventId == ev.Id)
            .ToListAsync();

        if (tables.Count != tableIds.Count)
            throw new KeyNotFoundException("One or more tables not found for this event");

        foreach (var table in tables)
        {
            if (table.Status != "Locked")
                throw new InvalidOperationException($"Table {table.Label} must be locked before booking");
            if (table.LockedByUserId != userId)
                throw new InvalidOperationException($"You do not hold table {table.Label}");
            if (table.LockExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException($"Lock on table {table.Label} has expired");
        }

        var pricing = await pricingService.ComputeForBookingAsync(
            new PricingQuoteRequest(ev.Id, TableIds: tableIds));
        var subtotal = pricing.SubtotalCents;
        var fee = pricing.FeeCents;
        var total = pricing.TotalCents;
        var piAmount = pricing.PaymentIntentAmountCents;
        var taxCalculationId = pricing.TaxCalculationId;
        var estimatedTaxCents = pricing.TaxCents;
        var totalSeats = tables.Sum(t => t.Capacity);

        var organizer = await context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == ev.OrganizerId);

        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            piAmount, subtotal, organizer?.StripeConnectedAccountId);

        // Create booking with the first table as primary (for backward compat)
        var bookingNumber = GenerateBookingNumber();
        var bookingId = await bookingProc.CreateBookingAsync(
            userId, ev.Id, tables[0].Id, totalSeats, null, subtotal, fee, total, bookingNumber);

        // Insert additional tables into junction table
        foreach (var table in tables.Skip(1))
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO booking_tables (\"BookingId\", \"TableId\") VALUES (@p0, @p1) ON CONFLICT DO NOTHING",
                bookingId, table.Id);
        }

        await stripeTransactionProc.CreateAsync(bookingId, intentId, piAmount, subtotal, taxCalculationId);

        var tableLabels = string.Join(", ", tables.Select(t => t.Label));
        Log.Information("[Booking] Created multi-table booking {BookingNumber} for tables [{Tables}], event {EventId}, total ${Total}, tax ${Tax}",
            bookingNumber, tableLabels, ev.Id, total / 100.0, estimatedTaxCents / 100.0);

        var dto = await GetByIdAsync(bookingId) ?? throw new InvalidOperationException("Booking creation failed");

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

    private async Task<BookingDto> CreateCapacityBookingAsync(Guid userId, CreateBookingRequest request, EventView ev)
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

        var pricing = await pricingService.ComputeForBookingAsync(
            new PricingQuoteRequest(ev.Id, SeatCount: seatsRequested, EventTicketTypeId: request.EventTicketTypeId));
        var subtotal = pricing.SubtotalCents;
        var fee = pricing.FeeCents;
        var total = pricing.TotalCents;
        var piAmount = pricing.PaymentIntentAmountCents;
        var taxCalculationId = pricing.TaxCalculationId;
        var estimatedTaxCents = pricing.TaxCents;

        var organizer = await context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == ev.OrganizerId);

        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            piAmount, subtotal, organizer?.StripeConnectedAccountId);

        var bookingNumber = GenerateBookingNumber();

        // sp_reserve_open_capacity serializes capacity + ticket-type quota checks via row-level
        // locks on events/event_ticket_types rows. Replaces the previous Redis-lock + SELECT +
        // INSERT pattern which could race under concurrent load.
        Guid bookingId;
        try
        {
            bookingId = await bookingProc.ReserveOpenCapacityAsync(
                userId, request.EventId, seatsRequested, request.EventTicketTypeId,
                subtotal, fee, total, bookingNumber);
        }
        catch (Exception ex) when (ex.Message.Contains("capacity") || ex.Message.Contains("availability"))
        {
            // Roll back the Stripe intent we just created so we don't orphan it
            try { await paymentService.RefundPaymentAsync(intentId); } catch { }
            throw new InvalidOperationException(ex.Message, ex);
        }

        await stripeTransactionProc.CreateAsync(bookingId, intentId, piAmount, subtotal, taxCalculationId);

        Log.Information("[Booking] Created capacity booking {BookingNumber} for {Seats} seats, event {EventId}, total ${Total}, tax ${Tax}",
            bookingNumber, seatsRequested, request.EventId, total / 100.0, estimatedTaxCents / 100.0);

        var dto = await GetByIdAsync(bookingId) ?? throw new InvalidOperationException("Booking creation failed");

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

    public async Task<BookingDto> ConfirmPaymentAsync(Guid bookingId, Guid userId)
    {
        var booking = await context.BookingViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("Not your booking");

        if (booking.Status != "Pending")
            throw new InvalidOperationException($"Cannot confirm booking in {booking.Status} status");

        if (booking.TableId.HasValue)
        {
            var table = await context.TableViews.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == booking.TableId.Value);
            if (table is null || table.Status != "Locked" || table.LockedByUserId != userId)
                throw new InvalidOperationException("Table lock has expired. Please select a new table.");
        }

        if (booking.PaymentIntentId is null)
            throw new InvalidOperationException("No payment associated with this booking");

        var intent = await paymentService.GetPaymentIntentAsync(booking.PaymentIntentId);
        if (intent.Status != "succeeded")
            throw new InvalidOperationException($"Payment has not succeeded (status: {intent.Status}). Please complete payment before confirming.");

        var expectedAmount = booking.PaymentAmountCents ?? booking.TotalCents;
        if (intent.AmountReceived != expectedAmount)
        {
            Log.Error(
                "[Booking] PAYMENT_AMOUNT_MISMATCH booking={BookingNumber} intent={IntentId} expected={Expected} received={Received}",
                booking.BookingNumber, booking.PaymentIntentId, expectedAmount, intent.AmountReceived);
            throw new InvalidOperationException("Payment amount does not match booking total");
        }

        await stripeTransactionProc.UpdateStatusAsync(booking.PaymentIntentId, "Succeeded");

        var qrToken = GenerateQrToken();
        await bookingProc.ConfirmBookingAsync(bookingId, qrToken);

        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var appName = await settings.GetOrDefaultAsync("app_name", "Code829") ?? "Code829";
        var checkinLink = $"{frontendUrl}/booking/{bookingId}/checkin";
        await emailService.SendAsync(
            booking.UserEmail,
            $"Booking Confirmed — {booking.EventTitle} | {appName}",
            EmailTemplates.BookingConfirmed(
                appName, booking.UserFirstName, booking.BookingNumber,
                booking.EventTitle, $"${booking.TotalCents / 100.0:F2}", checkinLink)
        );

        Log.Information("[Booking] Confirmed {BookingNumber}, QR: {QrToken}", booking.BookingNumber, qrToken);
        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto> CancelAsync(Guid bookingId, Guid userId)
    {
        var booking = await context.BookingViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("Not your booking");

        if (booking.Status is not ("Pending" or "Paid"))
            throw new InvalidOperationException($"Cannot cancel booking in {booking.Status} status");

        await bookingProc.CancelBookingAsync(bookingId);

        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto> RefundAsync(Guid bookingId)
    {
        var booking = await context.BookingViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.Status != "Paid")
            throw new InvalidOperationException($"Cannot refund booking in {booking.Status} status");

        if (booking.PaymentIntentId is not null)
            await paymentService.RefundPaymentAsync(booking.PaymentIntentId);

        // Reverse the tax transaction if one was recorded. This is accounting-critical —
        // a missing reversal means we'll over-report sales tax remitted. Log as Error (not
        // Warning) with a TAX_REVERSAL_FAILED marker so alerts can pattern-match it.
        if (!string.IsNullOrEmpty(booking.TaxTransactionId) && booking.PaymentIntentId is not null)
        {
            try
            {
                await taxService.CreateReversalAsync(booking.TaxTransactionId, $"{booking.PaymentIntentId}-refund");
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "[Booking] TAX_REVERSAL_FAILED booking={BookingNumber} txTxn={TaxTxnId} intent={IntentId} — refund completed but tax not reversed; manual reconciliation required",
                    booking.BookingNumber, booking.TaxTransactionId, booking.PaymentIntentId);
            }
        }

        await bookingProc.RefundBookingAsync(bookingId);

        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto?> GetByIdAsync(Guid bookingId)
    {
        var b = await context.BookingViews.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (b is null) return null;

        var venueAddress = !string.IsNullOrEmpty(b.VenueAddress)
            ? $"{b.VenueAddress}, {b.VenueCity}, {b.VenueState}"
            : null;

        return new BookingDto(
            b.Id, b.BookingNumber, b.Status,
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

    public async Task<byte[]> GetQrImageAsync(Guid bookingId, Guid userId)
    {
        var booking = await context.BookingViews.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("Not your booking");

        if (string.IsNullOrEmpty(booking.QrToken))
            throw new InvalidOperationException("No QR token — booking not yet confirmed");

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(booking.QrToken, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(10);
    }

    private static string GenerateBookingNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMdd");
        var random = RandomNumberGenerator.GetInt32(100000, 999999);
        return $"BK-{timestamp}-{random}";
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
