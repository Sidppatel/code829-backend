using System.Security.Cryptography;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;
using StackExchange.Redis;

namespace Api.Services;

public class BookingService(
    EventPlatformDbContext context,
    IBookingProcedures bookingProc,
    IStripeTransactionProcedures stripeTransactionProc,
    IPaymentService paymentService,
    IEmailService emailService,
    ISettingsService settings,
    IConnectionMultiplexer redis
) : IBookingService
{
    public async Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != "Published")
            throw new InvalidOperationException("Event is not available for booking");

        if (request.TableId.HasValue)
            return await CreateTableBookingAsync(userId, request, ev);

        if (request.SeatsReserved.HasValue)
            return await CreateCapacityBookingAsync(userId, request, ev);

        throw new InvalidOperationException("Either TableId (for Grid events) or SeatsReserved (for Open events) is required");
    }

    private async Task<BookingDto> CreateTableBookingAsync(Guid userId, CreateBookingRequest request, EventView ev)
    {
        if (ev.LayoutMode != "Grid")
            throw new InvalidOperationException("Table bookings are only available for Grid events");

        var table = await context.TableViews.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TableId!.Value && t.EventId == request.EventId)
            ?? throw new KeyNotFoundException("Table not found for this event");

        if (table.Status != "Locked")
            throw new InvalidOperationException("Table must be locked before booking");

        if (table.LockedByUserId != userId)
            throw new InvalidOperationException("You do not hold this table");

        if (table.LockExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Table lock has expired");

        var subtotal = table.PriceCents;
        var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_grid_cents", "2500") ?? "2500");
        var fee = table.PlatformFeeCents ?? defaultFeeCents;
        var total = subtotal + fee;

        var organizer = await context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == ev.OrganizerId);
        var stripeTaxEnabled = (await settings.GetOrDefaultAsync("stripe_tax_enabled", "false")) == "true";
        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            total, subtotal, organizer?.StripeConnectedAccountId, stripeTaxEnabled);

        var bookingNumber = GenerateBookingNumber();
        var bookingId = await bookingProc.CreateBookingAsync(
            userId, request.EventId, table.Id, null, null, subtotal, fee, total, bookingNumber);

        await stripeTransactionProc.CreateAsync(bookingId, intentId, total, subtotal);

        Log.Information("[Booking] Created table booking {BookingNumber} for table {TableLabel}, event {EventId}, total ${Total}",
            bookingNumber, table.Label, request.EventId, total / 100.0);

        var dto = await GetByIdAsync(bookingId) ?? throw new InvalidOperationException("Booking creation failed");
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

        var redisDb = redis.GetDatabase();
        var lockKey = $"capacity:{request.EventId}";
        var lockToken = Guid.NewGuid().ToString();
        var acquired = await redisDb.StringSetAsync(lockKey, lockToken, TimeSpan.FromSeconds(10), When.NotExists);
        if (!acquired)
            throw new InvalidOperationException("Another reservation is in progress. Please try again.");

        try
        {
            var totalReserved = await context.BookingViews.AsNoTracking()
                .Where(b => b.EventId == request.EventId
                    && (b.Status == "Pending" || b.Status == "Paid" || b.Status == "CheckedIn")
                    && b.SeatsReserved.HasValue)
                .SumAsync(b => b.SeatsReserved!.Value);

            if (totalReserved + seatsRequested > ev.MaxCapacity.Value)
                throw new InvalidOperationException(
                    $"Not enough capacity. Available: {ev.MaxCapacity.Value - totalReserved}, requested: {seatsRequested}");

            var pricePerPerson = selectedType?.PriceCents ?? ev.PricePerPersonCents!.Value;
            var subtotal = pricePerPerson * seatsRequested;
            var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_open_cents", "1000") ?? "1000");
            var feePerTicket = selectedType?.PlatformFeeCents ?? defaultFeeCents;
            var fee = feePerTicket * seatsRequested;
            var total = subtotal + fee;

            var organizer = await context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == ev.OrganizerId);
            var stripeTaxEnabled = (await settings.GetOrDefaultAsync("stripe_tax_enabled", "false")) == "true";
            var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
                total, subtotal, organizer?.StripeConnectedAccountId, stripeTaxEnabled);

            var bookingNumber = GenerateBookingNumber();
            var bookingId = await bookingProc.CreateBookingAsync(
                userId, request.EventId, null, seatsRequested, request.EventTicketTypeId, subtotal, fee, total, bookingNumber);

            await stripeTransactionProc.CreateAsync(bookingId, intentId, total, subtotal);

            Log.Information("[Booking] Created capacity booking {BookingNumber} for {Seats} seats, event {EventId}, total ${Total}",
                bookingNumber, seatsRequested, request.EventId, total / 100.0);

            var dto = await GetByIdAsync(bookingId) ?? throw new InvalidOperationException("Booking creation failed");
            return dto with { ClientSecret = clientSecret };
        }
        finally
        {
            var script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
            await redisDb.ScriptEvaluateAsync(script, [(RedisKey)lockKey], [(RedisValue)lockToken]);
        }
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

        var paymentStatus = await paymentService.ConfirmPaymentAsync(booking.PaymentIntentId);
        if (paymentStatus != "succeeded")
            throw new InvalidOperationException($"Payment has not succeeded (status: {paymentStatus}). Please complete payment before confirming.");

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
            b.SubtotalCents, b.FeeCents, b.TotalCents, b.QrToken,
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
