using System.Security.Cryptography;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Serilog;

namespace Api.Services;

public class BookingService(
    EventPlatformDbContext context,
    IPricingEngine pricingEngine,
    IPaymentService paymentService,
    IEmailService emailService,
    ISettingsService settings
) : IBookingService
{
    public async Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request)
    {
        var ev = await context.Events.FindAsync(request.EventId)
            ?? throw new KeyNotFoundException("Event not found");

        if (ev.Status != EventStatus.Published)
            throw new InvalidOperationException("Event is not available for booking");

        var maxTickets = int.Parse(
            await settings.GetOrDefaultAsync("max_tickets_per_booking", "10") ?? "10");
        if (request.Items.Count > maxTickets)
            throw new InvalidOperationException($"Maximum {maxTickets} tickets per booking");

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

            // If it's a seated booking, we might have table-specific fees
            if (item.SeatId.HasValue)
            {
                var seat = await context.Seats
                    .Include(s => s.Table)
                    .FirstOrDefaultAsync(s => s.Id == item.SeatId.Value);
                if (seat?.Table != null && seat.Table.PlatformFeeCents > 0)
                {
                    feePerItem = seat.Table.PlatformFeeCents;
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
        var (intentId, _) = await paymentService.CreatePaymentIntentAsync(total);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = GenerateBookingNumber(),
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
        Log.Information("[Booking] Created {BookingNumber} for event {EventId}, total ${Total}",
            booking.BookingNumber, request.EventId, total / 100.0);

        return await GetByIdAsync(booking.Id) ?? throw new InvalidOperationException("Booking creation failed");
    }

    public async Task<BookingDto> ConfirmPaymentAsync(Guid bookingId)
    {
        var booking = await context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Items).ThenInclude(i => i.TicketType)
            .Include(b => b.Event)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm booking in {booking.Status} status");

        // Confirm mock payment
        var paymentResult = await paymentService.ConfirmPaymentAsync(booking.Payment!.PaymentIntentId);
        booking.Payment.Status = PaymentStatus.Succeeded;
        booking.Payment.PaidAt = DateTime.UtcNow;

        booking.Status = BookingStatus.Paid;
        booking.QrToken = GenerateQrToken();

        // Generate per-item QR tokens and invitation tokens, increment sold counts
        foreach (var item in booking.Items)
        {
            item.QrToken = GenerateQrToken();
            item.InvitationToken = GenerateInvitationToken();
            item.TicketType.QuantitySold++;
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

        // Send confirmation email
        var brandName = await settings.GetOrDefaultAsync("brand_name", "Code829");
        await emailService.SendAsync(
            booking.User.Email,
            $"Booking Confirmed — {booking.Event.Title} | {brandName}",
            $"Hi {booking.User.FirstName},\n\n" +
            $"Your booking {booking.BookingNumber} for {booking.Event.Title} is confirmed!\n" +
            $"Total: ${booking.TotalCents / 100.0:F2}\n" +
            $"QR Token: {booking.QrToken}\n\n" +
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

        // Decrement sold counts
        foreach (var item in booking.Items)
        {
            item.TicketType.QuantitySold = Math.Max(0, item.TicketType.QuantitySold - 1);
        }

        await context.SaveChangesAsync();
        return (await GetByIdAsync(bookingId))!;
    }

    public async Task<BookingDto?> GetByIdAsync(Guid bookingId)
    {
        var b = await context.Bookings
            .Include(x => x.User)
            .Include(x => x.Event)
            .Include(x => x.Items).ThenInclude(i => i.TicketType)
            .Include(x => x.Items).ThenInclude(i => i.Seat)
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
                i.QrToken, i.GuestName, i.GuestEmail, i.InvitationToken, i.IsCheckedIn
            )).ToList(),
            b.Payment is not null ? new PaymentDto(
                b.Payment.Id, b.Payment.PaymentIntentId, b.Payment.Status.ToString(),
                b.Payment.AmountCents, b.Payment.PaidAt, b.Payment.RefundedAt
            ) : null,
            b.CreatedAt
        );
    }

    public async Task<byte[]> GetQrImageAsync(Guid bookingId)
    {
        var booking = await context.Bookings.FindAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

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

    private static string GenerateInvitationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
