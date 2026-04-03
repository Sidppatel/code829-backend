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
    private readonly Mock<IPaymentService> _paymentService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<ISettingsService> _settingsService;
    private readonly Mock<IConnectionMultiplexer> _redis;
    private readonly Mock<IDatabase> _redisDb;
    private readonly BookingService _service;
    private readonly Guid _userId;
    private readonly Guid _eventId;
    private readonly Guid _venueId;

    public BookingServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _paymentService = new Mock<IPaymentService>();
        _emailService = new Mock<IEmailService>();
        _settingsService = new Mock<ISettingsService>();
        _redis = new Mock<IConnectionMultiplexer>();
        _redisDb = new Mock<IDatabase>();

        _redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDb.Object);
        _settingsService.Setup(s => s.GetOrDefaultAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync("10");

        _service = new BookingService(_context, _paymentService.Object,
            _emailService.Object, _settingsService.Object, _redis.Object);

        _userId = Guid.NewGuid();
        _eventId = Guid.NewGuid();
        _venueId = Guid.NewGuid();

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
            Id = _venueId,
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
            LayoutMode = LayoutMode.Open,
            MaxCapacity = 100,
            PricePerPersonCents = 5000,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            VenueId = venue.Id,
            OrganizerId = _userId
        };
        _context.Events.Add(ev);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_WhenEventNotPublished_ThrowsInvalidOperationException()
    {
        var ev = await _context.Events.FindAsync(_eventId);
        ev!.Status = EventStatus.Draft;
        await _context.SaveChangesAsync();

        var request = new CreateBookingRequest(_eventId, SeatsReserved: 2);

        var act = () => _service.CreateAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available for booking*");
    }

    [Fact]
    public async Task CreateAsync_OpenEvent_ValidRequest_CreatesBookingWithCorrectTotals()
    {
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .ReturnsAsync(true);
        _redisDb.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(),
            It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(("pi_test_123", "pi_test_123_secret", "requires_confirmation"));

        var request = new CreateBookingRequest(_eventId, SeatsReserved: 2);

        var result = await _service.CreateAsync(_userId, request);

        result.Should().NotBeNull();
        result.Status.Should().Be("Pending");
        result.SubtotalCents.Should().Be(10000); // 2 * 5000
        result.SeatsReserved.Should().Be(2);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenNotOwner_ThrowsUnauthorizedAccessException()
    {
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .ReturnsAsync(true);
        _redisDb.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(),
            It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(("pi_test_123", "pi_test_123_secret", "requires_confirmation"));

        var request = new CreateBookingRequest(_eventId, SeatsReserved: 1);
        var booking = await _service.CreateAsync(_userId, request);

        var otherUserId = Guid.NewGuid();
        var act = () => _service.ConfirmPaymentAsync(booking.Id, otherUserId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Not your booking*");
    }

    [Fact]
    public async Task RefundAsync_WhenNotPaid_ThrowsInvalidOperationException()
    {
        _redisDb.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<When>()))
            .ReturnsAsync(true);
        _redisDb.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(),
            It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(("pi_test_123", "pi_test_123_secret", "requires_confirmation"));

        var request = new CreateBookingRequest(_eventId, SeatsReserved: 1);
        var booking = await _service.CreateAsync(_userId, request);

        var act = () => _service.RefundAsync(booking.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot refund*");
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyRefunded_ThrowsInvalidOperationException()
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingNumber = "BK-TEST-999999",
            Status = BookingStatus.Refunded,
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 5000,
            FeeCents = 0,
            TotalCents = 5000
        };
        _context.Bookings.Add(booking);
        _context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            PaymentIntentId = "pi_test_cancel",
            Status = PaymentStatus.Refunded,
            AmountCents = 5000,
            RefundedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var act = () => _service.CancelAsync(booking.Id, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    [Fact]
    public async Task CreateAsync_GridEvent_TableMustBeLocked()
    {
        var gridEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = "Grid Event",
            Slug = "grid-event",
            Status = EventStatus.Published,
            LayoutMode = LayoutMode.Grid,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            VenueId = _venueId,
            OrganizerId = _userId
        };
        _context.Events.Add(gridEvent);

        var eventTable = new EventTable
        {
            Id = Guid.NewGuid(),
            Label = "Standard Table",
            Capacity = 4,
            Shape = TableShape.Round,
            PriceCents = 10000,
            IsActive = true,
            EventId = gridEvent.Id
        };
        _context.EventTables.Add(eventTable);

        var table = new Table
        {
            Id = Guid.NewGuid(),
            Label = "T1",
            IsActive = true,
            Status = TableStatus.Available,
            SortOrder = 1,
            EventId = gridEvent.Id,
            EventTableId = eventTable.Id
        };
        _context.Tables.Add(table);
        await _context.SaveChangesAsync();

        var request = new CreateBookingRequest(gridEvent.Id, TableId: table.Id);

        var act = () => _service.CreateAsync(_userId, request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be locked*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
