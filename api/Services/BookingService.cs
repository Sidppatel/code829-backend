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
    IPricingEngine pricingEngine,
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

        // Route to the appropriate booking flow
        if (request.TableId.HasValue)
            return await CreateTableBookingAsync(userId, request, ev);

        if (request.SeatsReserved.HasValue)
            return await CreateCapacityBookingAsync(userId, request, ev);

        return await CreateSeatBookingAsync(userId, request, ev);
    }

    private async Task<BookingDto> CreateTableBookingAsync(Guid userId, CreateBookingRequest request, Event ev)
    {
        var table = await context.Tables
            .Include(t => t.Seats)
            .Include(t => t.TableType)
            .FirstOrDefaultAsync(t => t.Id == request.TableId!.Value && t.EventId == request.EventId)
            ?? throw new KeyNotFoundException("Table not found for this event");

        if (table.Status != TableStatus.Locked)
            throw new InvalidOperationException("Table must be locked before booking");

        if (table.LockedByUserId != userId)
            throw new InvalidOperationException("You do not hold this table");

        if (table.LockExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Table lock has expired");

        var ticketTypeId = request.TicketTypeId
            ?? throw new InvalidOperationException("TicketTypeId is required for table bookings");

        var tt = await context.TicketTypes.FindAsync(ticketTypeId)
            ?? throw new KeyNotFoundException("Ticket type not found");

        if (tt.EventId != request.EventId)
            throw new InvalidOperationException("Ticket type does not belong to this event");

        // Calculate price based on table pricing
        var tablePrice = table.PriceOverrideCents ?? table.PriceCents;
        var feePerItem = table.TableType?.PlatformFeeCents ?? tt.PlatformFeeCents ?? 0;

        int subtotal, fee;
        var bookingItems = new List<BookingItem>();

        if (table.PriceType == PriceType.PerTable)
        {
            subtotal = tablePrice;
            fee = feePerItem;
            // One booking item for the whole table, linked to the first seat
            var firstSeat = table.Seats.OrderBy(s => s.SeatNumber).FirstOrDefault();
            bookingItems.Add(new BookingItem
            {
                Id = Guid.NewGuid(),
                TicketTypeId = ticketTypeId,
                SeatId = firstSeat?.Id,
                PriceCents = tablePrice
            });
        }
        else
        {
            // PerSeat: one booking item per seat
            var seatCount = table.Seats.Count;
            subtotal = tablePrice * seatCount;
            fee = feePerItem * seatCount;
            foreach (var seat in table.Seats.OrderBy(s => s.SeatNumber))
            {
                bookingItems.Add(new BookingItem
                {
                    Id = Guid.NewGuid(),
                    TicketTypeId = ticketTypeId,
                    SeatId = seat.Id,
                    PriceCents = tablePrice
                });
            }
        }

        var total = subtotal + fee;
        var (intentId, _, _) = await paymentService.CreatePaymentIntentAsync(total);

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

        foreach (var bi in bookingItems)
            bi.BookingId = booking.Id;

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.Bookings.Add(booking);
            context.BookingItems.AddRange(bookingItems);
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

        return await GetByIdAsync(booking.Id) ?? throw new InvalidOperationException("Booking creation failed");
    }

    private async Task<BookingDto> CreateCapacityBookingAsync(Guid userId, CreateBookingRequest request, Event ev)
    {
        if (ev.LayoutMode != LayoutMode.CapacityOnly)
            throw new InvalidOperationException("Capacity reservations are only available for open-layout events");

        if (!ev.MaxCapacity.HasValue || ev.MaxCapacity <= 0)
            throw new InvalidOperationException("Event has no capacity configured");

        var seatsRequested = request.SeatsReserved!.Value;
        var ticketTypeId = request.TicketTypeId
            ?? throw new InvalidOperationException("TicketTypeId is required for capacity bookings");

        var tt = await context.TicketTypes.FindAsync(ticketTypeId)
            ?? throw new KeyNotFoundException("Ticket type not found");

        if (tt.EventId != request.EventId)
            throw new InvalidOperationException("Ticket type does not belong to this event");

        // Use Redis lock to serialize capacity checks
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

            var price = tt.PriceCents ?? 0;
            var feePerSeat = tt.PlatformFeeCents ?? 0;
            var subtotal = price * seatsRequested;
            var fee = feePerSeat * seatsRequested;
            var total = subtotal + fee;

            var (intentId, _, _) = await paymentService.CreatePaymentIntentAsync(total);

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

            var bookingItems = new List<BookingItem>();
            for (var i = 0; i < seatsRequested; i++)
            {
                bookingItems.Add(new BookingItem
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    TicketTypeId = ticketTypeId,
                    PriceCents = price
                });
            }

            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Bookings.Add(booking);
                context.BookingItems.AddRange(bookingItems);
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

            return await GetByIdAsync(booking.Id) ?? throw new InvalidOperationException("Booking creation failed");
        }
        finally
        {
            var script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
            await redisDb.ScriptEvaluateAsync(script, [(RedisKey)lockKey], [(RedisValue)lockToken]);
        }
    }

    private async Task<BookingDto> CreateSeatBookingAsync(Guid userId, CreateBookingRequest request, Event ev)
    {
        var maxTickets = int.Parse(
            await settings.GetOrDefaultAsync("max_tickets_per_booking", "10") ?? "10");
        if (request.Items.Count > maxTickets)
            throw new InvalidOperationException($"Maximum {maxTickets} tickets per booking");

        // Acquire distributed locks for seated bookings to prevent TOCTOU race conditions
        var db = redis.GetDatabase();
        var acquiredLocks = new List<(string Key, string Token)>();
        try
        {
            // Resolve ticket prices and fees
            var items = new List<(int Price, int Fee)>();
            var bookingItems = new List<BookingItem>();
            foreach (var item in request.Items)
            {
                var tt = await context.TicketTypes.FindAsync(item.TicketTypeId)
                    ?? throw new KeyNotFoundException($"Ticket type {item.TicketTypeId} not found");

                if (tt.EventId != request.EventId)
                    throw new InvalidOperationException("Ticket type does not belong to this event");

                if (tt.QuantitySold >= tt.QuantityTotal)
                    throw new InvalidOperationException($"Ticket type '{tt.Name}' is sold out");

                var price = tt.PriceCents ?? 0;
                var feePerItem = tt.PlatformFeeCents ?? 0;

                // If it's a seated booking, acquire lock and validate seat
                if (item.SeatId.HasValue)
                {
                    var lockKey = $"seat-lock:{item.SeatId.Value}";
                    var lockToken = Guid.NewGuid().ToString();
                    var acquired = await db.StringSetAsync(lockKey, lockToken, TimeSpan.FromSeconds(30), When.NotExists);
                    if (!acquired)
                        throw new InvalidOperationException("One or more selected seats are being reserved by another user. Please try again.");
                    acquiredLocks.Add((lockKey, lockToken));

                    // Check if seat is already booked by another booking
                    var seatAlreadyBooked = await context.BookingItems
                        .AnyAsync(bi => bi.SeatId == item.SeatId.Value
                            && bi.Booking.EventId == request.EventId
                            && (bi.Booking.Status == BookingStatus.Paid || bi.Booking.Status == BookingStatus.CheckedIn));
                    if (seatAlreadyBooked)
                        throw new InvalidOperationException("One or more selected seats are already booked");

                    var seat = await context.Seats
                        .Include(s => s.Table)
                            .ThenInclude(t => t!.TableType)
                        .FirstOrDefaultAsync(s => s.Id == item.SeatId.Value);
                    if (seat?.Table?.TableType != null && seat.Table.TableType.PlatformFeeCents > 0)
                    {
                        feePerItem = seat.Table.TableType.PlatformFeeCents;
                    }
                }

                items.Add((price, feePerItem));
                bookingItems.Add(new BookingItem
                {
                    Id = Guid.NewGuid(),
                    TicketTypeId = item.TicketTypeId,
                    SeatId = item.SeatId,
                    PriceCents = price
                });
            }

        var (subtotal, fee, total) = await pricingEngine.CalculateAsync(request.EventId, items);

        // Create payment intent
        var (intentId, _, _) = await paymentService.CreatePaymentIntentAsync(total);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = await GenerateBookingNumberAsync(),
            Status = BookingStatus.Pending,
            UserId = userId,
            EventId = request.EventId,
            SubtotalCents = subtotal,
            FeeCents = fee,
            TotalCents = total
        };

        foreach (var bi in bookingItems)
        {
            bi.BookingId = booking.Id;
        }

        // Use explicit transaction to prevent partial writes
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            context.Bookings.Add(booking);
            context.BookingItems.AddRange(bookingItems);
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
            Log.Information("[Booking] Created {BookingNumber} for event {EventId}, total ${Total}",
                booking.BookingNumber, request.EventId, total / 100.0);

            return await GetByIdAsync(booking.Id) ?? throw new InvalidOperationException("Booking creation failed");
        }
        finally
        {
            // Release all acquired seat locks
            foreach (var (lockKey, lockToken) in acquiredLocks)
            {
                var script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
                await db.ScriptEvaluateAsync(script, [(RedisKey)lockKey], [(RedisValue)lockToken]);
            }
        }
    }

    public async Task<BookingDto> ConfirmPaymentAsync(Guid bookingId, Guid userId)
    {
        var booking = await context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Items).ThenInclude(i => i.TicketType)
            .Include(b => b.Event)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.UserId != userId)
            throw new UnauthorizedAccessException("Not your booking");

        if (booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm booking in {booking.Status} status");

        // For table bookings, verify the table lock is still valid
        if (booking.TableId.HasValue)
        {
            var table = await context.Tables.FindAsync(booking.TableId.Value);
            if (table is null || table.Status != TableStatus.Locked || table.LockedByUserId != userId)
                throw new InvalidOperationException("Table lock has expired. Please select a new table.");
        }

        // Use explicit transaction for atomicity
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Confirm mock payment
            var paymentResult = await paymentService.ConfirmPaymentAsync(booking.Payment!.PaymentIntentId);
            booking.Payment.Status = PaymentStatus.Succeeded;
            booking.Payment.PaidAt = DateTime.UtcNow;

            booking.Status = BookingStatus.Paid;
            booking.QrToken = GenerateQrToken();

            // Generate per-item QR tokens and invitation tokens
            foreach (var item in booking.Items)
            {
                item.QrToken = GenerateQrToken();
                item.InvitationToken = GenerateInvitationToken();
            }

            // Atomically increment sold counts to prevent lost-update race condition
            foreach (var item in booking.Items)
            {
                await context.TicketTypes
                    .Where(t => t.Id == item.TicketTypeId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.QuantitySold, t => t.QuantitySold + 1));
            }

            // Mark table as permanently booked if this is a table booking
            if (booking.TableId.HasValue)
            {
                var tableToBook = await context.Tables.FindAsync(booking.TableId.Value);
                if (tableToBook is not null)
                {
                    tableToBook.Status = TableStatus.Booked;
                    tableToBook.LockedByUserId = null;
                    tableToBook.LockExpiresAt = null;
                    tableToBook.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Release any seat holds for this user+event
            var holds = await context.SeatHolds
                .Where(h => h.UserId == booking.UserId && h.EventId == booking.EventId && h.IsActive)
                .ToListAsync();
            foreach (var hold in holds)
            {
                hold.IsActive = false;
                hold.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Send confirmation email — QR token is never sent in plaintext
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

        // Release table lock if this is a pending table booking
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
            .Include(b => b.Items).ThenInclude(i => i.TicketType)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.Status != BookingStatus.Paid)
            throw new InvalidOperationException($"Cannot refund booking in {booking.Status} status");

        await paymentService.RefundPaymentAsync(booking.Payment!.PaymentIntentId);
        booking.Payment.Status = PaymentStatus.Refunded;
        booking.Payment.RefundedAt = DateTime.UtcNow;
        booking.Status = BookingStatus.Refunded;

        await context.SaveChangesAsync();

        // Atomically decrement sold counts to prevent lost-update race condition
        foreach (var item in booking.Items)
        {
            await context.TicketTypes
                .Where(t => t.Id == item.TicketTypeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.QuantitySold, t => t.QuantitySold - 1 < 0 ? 0 : t.QuantitySold - 1));
        }
        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto?> GetByIdAsync(Guid bookingId)
    {
        var b = await context.Bookings
            .Include(x => x.User)
            .Include(x => x.Event)
            .Include(x => x.Items).ThenInclude(i => i.TicketType)
            .Include(x => x.Items).ThenInclude(i => i.Seat).ThenInclude(s => s!.Table)
            .Include(x => x.Payment)
            .FirstOrDefaultAsync(x => x.Id == bookingId);

        if (b is null) return null;

        return new BookingDto(
            b.Id, b.BookingNumber, b.Status.ToString(),
            b.UserId, $"{b.User.FirstName} {b.User.LastName}", b.EventId, b.Event.Title,
            b.SubtotalCents, b.FeeCents, b.TotalCents, b.QrToken,
            b.Items.Select(i => new BookingItemDto(
                i.Id, i.TicketTypeId, i.TicketType.Name ?? "",
                i.SeatId, i.Seat?.Label, i.PriceCents,
                i.QrToken, i.GuestName, i.GuestEmail, i.InvitationToken, i.IsCheckedIn,
                i.Seat?.Table?.Label
            )).ToList(),
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
        // Fallback: use a longer random segment for guaranteed uniqueness
        var fallbackRandom = RandomNumberGenerator.GetInt32(100000000, 999999999);
        return $"BK-{timestamp}-{fallbackRandom}";
    }

    private static string GenerateQrToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"QR-{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }

    private static string GenerateInvitationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
