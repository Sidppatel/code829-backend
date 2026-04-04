using System.Security.Cryptography;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;
using StackExchange.Redis;

namespace Api.Services;

public class BookingService(
    EventPlatformDbContext context,
    IPaymentService paymentService,
    IEmailService emailService,
    ISettingsService settings,
    IConnectionMultiplexer redis
) : IBookingService
{
    public async Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request)
    {
        var ev = await context.Events.FindAsync(request.EventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != EventStatus.Published)
            throw new InvalidOperationException("Event is not available for booking");

        if (request.TableId.HasValue)
            return await CreateTableBookingAsync(userId, request, ev);

        if (request.SeatsReserved.HasValue)
            return await CreateCapacityBookingAsync(userId, request, ev);

        throw new InvalidOperationException("Either TableId (for Grid events) or SeatsReserved (for Open events) is required");
    }

    private async Task<BookingDto> CreateTableBookingAsync(Guid userId, CreateBookingRequest request, Event ev)
    {
        if (ev.LayoutMode != LayoutMode.Grid)
            throw new InvalidOperationException("Table bookings are only available for Grid events");

        var table = await context.Tables
            .Include(t => t.EventTable)
            .FirstOrDefaultAsync(t => t.Id == request.TableId!.Value && t.EventId == request.EventId)
            ?? throw new KeyNotFoundException("Table not found for this event");

        if (table.Status != TableStatus.Locked)
            throw new InvalidOperationException("Table must be locked before booking");

        if (table.LockedByUserId != userId)
            throw new InvalidOperationException("You do not hold this table");

        if (table.LockExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Table lock has expired");

        var subtotal = table.EventTable.PriceCents;
        var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_cents", "1500") ?? "1500");
        var fee = table.EventTable.PlatformFeeCents ?? ev.PlatformFeeCents ?? defaultFeeCents;
        var total = subtotal + fee;

        var organizer = await context.Users.FindAsync(ev.OrganizerId);
        var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
            total, fee, organizer?.StripeConnectedAccountId);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = await GenerateBookingNumberAsync(),
            Status = BookingStatus.Pending,
            UserId = userId,
            EventId = request.EventId,
            TableId = table.Id,
            SubtotalCents = subtotal,
            FeeCents = fee,
            TotalCents = total
        };

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.Bookings.Add(booking);
            context.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                PaymentIntentId = intentId,
                Status = PaymentStatus.RequiresConfirmation,
                AmountCents = total
            });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        Log.Information("[Booking] Created table booking {BookingNumber} for table {TableLabel}, event {EventId}, total ${Total}",
            booking.BookingNumber, table.Label, request.EventId, total / 100.0);

        var dto = await GetByIdAsync(booking.Id) ?? throw new InvalidOperationException("Booking creation failed");
        return dto with { ClientSecret = clientSecret };
    }

    private async Task<BookingDto> CreateCapacityBookingAsync(Guid userId, CreateBookingRequest request, Event ev)
    {
        if (ev.LayoutMode != LayoutMode.Open)
            throw new InvalidOperationException("Capacity reservations are only available for Open events");

        if (!ev.MaxCapacity.HasValue || ev.MaxCapacity <= 0)
            throw new InvalidOperationException("Event has no capacity configured");

        if (!ev.PricePerPersonCents.HasValue)
            throw new InvalidOperationException("Event has no price per person configured");

        var seatsRequested = request.SeatsReserved!.Value;

        var redisDb = redis.GetDatabase();
        var lockKey = $"capacity:{request.EventId}";
        var lockToken = Guid.NewGuid().ToString();
        var acquired = await redisDb.StringSetAsync(lockKey, lockToken, TimeSpan.FromSeconds(10), When.NotExists);
        if (!acquired)
            throw new InvalidOperationException("Another reservation is in progress. Please try again.");

        try
        {
            var activeStatuses = new[] { BookingStatus.Pending, BookingStatus.Paid, BookingStatus.CheckedIn };
            var totalReserved = await context.Bookings
                .Where(b => b.EventId == request.EventId
                    && activeStatuses.Contains(b.Status)
                    && b.SeatsReserved.HasValue)
                .SumAsync(b => b.SeatsReserved!.Value);

            if (totalReserved + seatsRequested > ev.MaxCapacity.Value)
                throw new InvalidOperationException(
                    $"Not enough capacity. Available: {ev.MaxCapacity.Value - totalReserved}, requested: {seatsRequested}");

            var pricePerPerson = ev.PricePerPersonCents.Value;
            var subtotal = pricePerPerson * seatsRequested;
            var defaultFeeCents = int.Parse(await settings.GetOrDefaultAsync("default_platform_fee_cents", "1500") ?? "1500");
            var fee = ev.PlatformFeeCents ?? defaultFeeCents;
            var total = subtotal + fee;

            var organizer = await context.Users.FindAsync(ev.OrganizerId);
            var (intentId, clientSecret, _) = await paymentService.CreatePaymentIntentAsync(
                total, fee, organizer?.StripeConnectedAccountId);

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                BookingNumber = await GenerateBookingNumberAsync(),
                Status = BookingStatus.Pending,
                UserId = userId,
                EventId = request.EventId,
                SeatsReserved = seatsRequested,
                SubtotalCents = subtotal,
                FeeCents = fee,
                TotalCents = total
            };

            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Bookings.Add(booking);
                context.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    PaymentIntentId = intentId,
                    Status = PaymentStatus.RequiresConfirmation,
                    AmountCents = total
                });
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            Log.Information("[Booking] Created capacity booking {BookingNumber} for {Seats} seats, event {EventId}, total ${Total}",
                booking.BookingNumber, seatsRequested, request.EventId, total / 100.0);

            var dto = await GetByIdAsync(booking.Id) ?? throw new InvalidOperationException("Booking creation failed");
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
        var booking = await context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Event)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("Not your booking");

        if (booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm booking in {booking.Status} status");

        if (booking.TableId.HasValue)
        {
            var table = await context.Tables.FindAsync(booking.TableId.Value);
            if (table is null || table.Status != TableStatus.Locked || table.LockedByUserId != userId)
                throw new InvalidOperationException("Table lock has expired. Please select a new table.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await paymentService.ConfirmPaymentAsync(booking.Payment!.PaymentIntentId);
            booking.Payment.Status = PaymentStatus.Succeeded;
            booking.Payment.PaidAt = DateTime.UtcNow;

            booking.Status = BookingStatus.Paid;
            booking.QrToken = GenerateQrToken();

            // Determine seat count and generate per-seat tickets
            var seatCount = booking.SeatsReserved ?? 1;
            if (booking.TableId.HasValue)
            {
                var tableToBook = await context.Tables
                    .Include(t => t.EventTable)
                    .FirstOrDefaultAsync(t => t.Id == booking.TableId.Value);
                if (tableToBook is not null)
                {
                    seatCount = tableToBook.EventTable.Capacity;
                    tableToBook.Status = TableStatus.Booked;
                    tableToBook.LockedByUserId = null;
                    tableToBook.LockExpiresAt = null;
                    tableToBook.UpdatedAt = DateTime.UtcNow;
                }
            }

            var timestamp = DateTime.UtcNow.ToString("yyMMdd");
            for (var seat = 1; seat <= seatCount; seat++)
            {
                var ticket = new BookingTicket
                {
                    Id = Guid.NewGuid(),
                    TicketCode = $"TK-{timestamp}-{RandomNumberGenerator.GetInt32(100000, 999999)}",
                    QrToken = GenerateQrToken(),
                    SeatNumber = seat,
                    BookingId = booking.Id,
                    Status = seat == 1 ? TicketStatus.Claimed : TicketStatus.Unassigned,
                    GuestUserId = seat == 1 ? booking.UserId : null,
                    ClaimedAt = seat == 1 ? DateTime.UtcNow : null,
                };
                context.BookingTickets.Add(ticket);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        var brandName = await settings.GetOrDefaultAsync("brand_name", "Code829");
        var frontendUrl = await settings.GetOrDefaultAsync("frontend_url", "http://localhost:5173");
        var checkinLink = $"{frontendUrl}/booking/{booking.Id}/checkin";
        await emailService.SendAsync(
            booking.User.Email,
            $"Booking Confirmed — {booking.Event.Title} | {brandName}",
            $"Hi {booking.User.FirstName},\n\n" +
            $"Your booking {booking.BookingNumber} for {booking.Event.Title} is confirmed!\n" +
            $"Total: ${booking.TotalCents / 100.0:F2}\n\n" +
            $"View your check-in QR code: {checkinLink}\n\n" +
            $"Show your QR code at the venue for check-in.\n\n" +
            $"— {brandName}"
        );

        Log.Information("[Booking] Confirmed {BookingNumber}, QR: {QrToken}", booking.BookingNumber, booking.QrToken);
        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto> CancelAsync(Guid bookingId, Guid userId)
    {
        var booking = await context.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("Not your booking");

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Paid))
            throw new InvalidOperationException($"Cannot cancel booking in {booking.Status} status");

        if (booking.TableId.HasValue && booking.Status == BookingStatus.Pending)
        {
            var table = await context.Tables.FindAsync(booking.TableId.Value);
            if (table is not null && table.Status == TableStatus.Locked && table.LockedByUserId == booking.UserId)
            {
                table.Status = TableStatus.Available;
                table.LockedByUserId = null;
                table.LockExpiresAt = null;
                table.UpdatedAt = DateTime.UtcNow;
            }
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto> RefundAsync(Guid bookingId)
    {
        var booking = await context.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.Status != BookingStatus.Paid)
            throw new InvalidOperationException($"Cannot refund booking in {booking.Status} status");

        await paymentService.RefundPaymentAsync(booking.Payment!.PaymentIntentId);
        booking.Payment.Status = PaymentStatus.Refunded;
        booking.Payment.RefundedAt = DateTime.UtcNow;
        booking.Status = BookingStatus.Refunded;

        if (booking.TableId.HasValue)
        {
            var table = await context.Tables.FindAsync(booking.TableId.Value);
            if (table is not null && table.Status == TableStatus.Booked)
            {
                table.Status = TableStatus.Available;
                table.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto?> GetByIdAsync(Guid bookingId)
    {
        var b = await context.Bookings
            .Include(x => x.User)
            .Include(x => x.Event).ThenInclude(e => e.Venue).ThenInclude(v => v!.Address)
            .Include(x => x.Table)
                .ThenInclude(t => t!.EventTable)
            .Include(x => x.Payment)
            .Include(x => x.Tickets)
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (b is null) return null;

        var venue = b.Event.Venue;
        var addr = venue?.Address;
        var venueAddress = addr is not null
            ? $"{addr.Line1}, {addr.City}, {addr.State} {addr.ZipCode}"
            : null;

        return new BookingDto(
            b.Id, b.BookingNumber, b.Status.ToString(),
            b.UserId, $"{b.User.FirstName} {b.User.LastName}", b.EventId, b.Event.Title,
            b.Event.StartDate, b.Event.EndDate, b.Event.Category.ToString(), b.Event.ImagePath,
            venue?.Name, venueAddress,
            b.SubtotalCents, b.FeeCents, b.TotalCents, b.QrToken,
            b.TableId, b.Table?.Label, b.SeatsReserved,
            b.Tickets.Count,
            b.Payment is not null ? new PaymentDto(
                b.Payment.Id, b.Payment.PaymentIntentId, b.Payment.Status.ToString(),
                b.Payment.AmountCents, b.Payment.PaidAt, b.Payment.RefundedAt
            ) : null,
            b.CreatedAt
        );
    }

    public async Task<byte[]> GetQrImageAsync(Guid bookingId, Guid userId)
    {
        var booking = await context.Bookings.FindAsync(bookingId)
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


    private async Task<string> GenerateBookingNumberAsync()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMdd");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var random = RandomNumberGenerator.GetInt32(100000, 999999);
            var candidate = $"BK-{timestamp}-{random}";
            var exists = await context.Bookings.AnyAsync(b => b.BookingNumber == candidate);
            if (!exists) return candidate;
        }
        var fallbackRandom = RandomNumberGenerator.GetInt32(100000000, 999999999);
        return $"BK-{timestamp}-{fallbackRandom}";
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
