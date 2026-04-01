using Api.Services;
using Contracts.DTOs.Bookings;
using Contracts.Enums;
using Db;
using Db.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;

namespace Api.Tests;

public class BookingServiceTests : IDisposable
{
    private readonly EventPlatformDbContext _context;
    private readonly Mock<IPricingEngine> _pricingEngine;
    private readonly Mock<IPaymentService> _paymentService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<ISettingsService> _settingsService;
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly BookingService _service;
    private readonly Guid _userId;
    private readonly Guid _eventId;
    private readonly Guid _ticketTypeId;

    public BookingServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _pricingEngine = new Mock<IPricingEngine>();
        _paymentService = new Mock<IPaymentService>();
        _emailService = new Mock<IEmailService>();
        _settingsService = new Mock<ISettingsService>();
        _redis = new Mock<IConnectionMultiplexer>();
        _redisDb = new Mock<IDatabase>();

        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDb.Object);
        _settingsService.Setup(s => s.GetOrDefaultAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("10");

        _service = new BookingService(_context, _pricingEngine.Object, _paymentService.Object,
            _emailService.Object, _settingsService.Object, _redis.Object);

        _userId = Guid.NewGuid();
        _eventId = Guid.NewGuid();
        _ticketTypeId = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var user = new User
        {
            Id = _userId,
            Email = "test@example.com",
            EmailHash = "hash",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.User,
            IsActive = true
        };
        _context.Users.Add(user);

        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Test Venue",
            IsActive = true
        };
        _context.Venues.Add(venue);

        var ev = new Event
        {
            Id = _eventId,
            Title = "Test Event",
            Slug = "test-event",
            Status = EventStatus.Published,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            VenueId = venue.Id,
            OrganizerId = _userId
        };
        _context.Events.Add(ev);

        var tt = new TicketType
        {
            Id = _ticketTypeId,
            EventId = _eventId,
            Name = "General Admission",
            PriceCents = 5000,
            QuantityTotal = 100,
            QuantitySold = 0,
            SortOrder = 1
        };
        _context.TicketTypes.Add(tt);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_WhenEventNotPublished_ThrowsInvalidOperationException()
    {
        var ev = await _context.Events.FindAsync(_eventId);
        ev!.Status = EventStatus.Draft;
        await _context.SaveChangesAsync();

        var request = new CreateBookingRequest(_eventId,
            [new BookingItemRequest(_ticketTypeId, null)]);

        var act = () => _service.CreateAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available for booking*");
    }

    [Fact]
    public async Task CreateAsync_WhenTicketSoldOut_ThrowsInvalidOperationException()
    {
        var tt = await _context.TicketTypes.FindAsync(_ticketTypeId);
        tt!.QuantitySold = tt.QuantityTotal;
        await _context.SaveChangesAsync();

        var request = new CreateBookingRequest(_eventId,
            [new BookingItemRequest(_ticketTypeId, null)]);

        var act = () => _service.CreateAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sold out*");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesBookingWithCorrectTotals()
    {
        _pricingEngine.Setup(p => p.CalculateAsync(It.IsAny<Guid>(), It.IsAny<List<(int, int)>>()))
            .ReturnsAsync((5000, 400, 5400));
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(("pi_test_123", "pi_test_123_secret", "requires_confirmation"));

        var request = new CreateBookingRequest(_eventId,
            [new BookingItemRequest(_ticketTypeId, null)]);

        var result = await _service.CreateAsync(_userId, request);

        result.Should().NotBeNull();
        result.Status.Should().Be("Pending");
        result.SubtotalCents.Should().Be(5000);
        result.FeeCents.Should().Be(400);
        result.TotalCents.Should().Be(5400);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
    {
        _pricingEngine.Setup(p => p.CalculateAsync(It.IsAny<Guid>(), It.IsAny<List<(int, int)>>()))
            .ReturnsAsync((5000, 400, 5400));
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(("pi_test_123", "pi_test_123_secret", "requires_confirmation"));

        var request = new CreateBookingRequest(_eventId,
            [new BookingItemRequest(_ticketTypeId, null)]);
        var booking = await _service.CreateAsync(_userId, request);

        var otherUserId = Guid.NewGuid();
        var act = () => _service.ConfirmPaymentAsync(booking.Id, otherUserId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Not your booking*");
    }

    [Fact]
    public async Task RefundAsync_WhenNotPaid_ThrowsInvalidOperationException()
    {
        _pricingEngine.Setup(p => p.CalculateAsync(It.IsAny<Guid>(), It.IsAny<List<(int, int)>>()))
            .ReturnsAsync((5000, 400, 5400));
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(("pi_test_123", "pi_test_123_secret", "requires_confirmation"));

        var request = new CreateBookingRequest(_eventId,
            [new BookingItemRequest(_ticketTypeId, null)]);
        var booking = await _service.CreateAsync(_userId, request);

        var act = () => _service.RefundAsync(booking.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot refund*");
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyRefunded_ThrowsInvalidOperationException()
    {
        // Create and manually set booking to Refunded
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = "BK-TEST-999999",
            Status = BookingStatus.Refunded,
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 5000,
            FeeCents = 400,
            TotalCents = 5400
        };
        _context.Bookings.Add(booking);
        _context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            PaymentIntentId = "pi_test_cancel",
            Status = PaymentStatus.Refunded,
            AmountCents = 5400,
            RefundedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var act = () => _service.CancelAsync(booking.Id, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    [Fact]
    public async Task CreateAsync_WhenSeatAlreadyBooked_ThrowsInvalidOperationException()
    {
        // Create a table and seat
        var tableType = new TableType
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            DefaultCapacity = 4,
            DefaultShape = TableShape.Round,
            DefaultPriceCents = 5000,
            PlatformFeeCents = 0,
            IsActive = true
        };
        _context.TableTypes.Add(tableType);

        var venue = await _context.Venues.FirstAsync();
        var table = new Table
        {
            Id = Guid.NewGuid(),
            Label = "T1",
            PriceType = PriceType.PerSeat,
            PriceCents = 5000,
            IsActive = true,
            SortOrder = 1,
            TableTypeId = tableType.Id,
            VenueId = venue.Id
        };
        _context.Tables.Add(table);

        var seat = new Seat
        {
            Id = Guid.NewGuid(),
            Label = "S1",
            SeatNumber = 1,
            TableId = table.Id
        };
        _context.Seats.Add(seat);

        // Create existing booked booking item for this seat
        var existingBooking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = "BK-TEST-111111",
            Status = BookingStatus.Paid,
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 5000,
            FeeCents = 0,
            TotalCents = 5000
        };
        _context.Bookings.Add(existingBooking);
        _context.BookingItems.Add(new BookingItem
        {
            Id = Guid.NewGuid(),
            BookingId = existingBooking.Id,
            TicketTypeId = _ticketTypeId,
            SeatId = seat.Id,
            PriceCents = 5000
        });
        await _context.SaveChangesAsync();

        // Mock Redis lock — NOT acquired (another user is holding the lock)
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var request = new CreateBookingRequest(_eventId,
            [new BookingItemRequest(_ticketTypeId, seat.Id)]);

        var act = () => _service.CreateAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*being reserved*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
