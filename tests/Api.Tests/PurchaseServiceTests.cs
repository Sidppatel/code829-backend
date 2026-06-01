using Api.Services;
using Contracts.DTOs.Purchases;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests;

public class PurchaseServiceTests : IDisposable
{
    private readonly EventPlatformDbContext _context;
    private readonly Mock<IPurchaseProcedures> _purchaseProc;
    private readonly Mock<IStripeTransactionProcedures> _stripeTransactionProc;
    private readonly Mock<IPaymentService> _paymentService;
    private readonly Mock<ITaxService> _taxService;
    private readonly Mock<IPricingService> _pricingService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<ISettingsService> _settingsService;
    private readonly PurchaseService _service;
    private readonly Guid _userId;
    private readonly Guid _eventId;
    private readonly Guid _venueId;

    public PurchaseServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _purchaseProc = new Mock<IPurchaseProcedures>();
        _stripeTransactionProc = new Mock<IStripeTransactionProcedures>();
        _paymentService = new Mock<IPaymentService>();
        _taxService = new Mock<ITaxService>();
        _pricingService = new Mock<IPricingService>();
        _emailService = new Mock<IEmailService>();
        _settingsService = new Mock<ISettingsService>();
        var orgProcMock = new Mock<IOrganizationProcedures>();

        _settingsService.Setup(s => s.GetOrDefaultAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("10");

        var enrichmentMock = new Mock<IPaymentEnrichmentService>();
        _service = new PurchaseService(_context, _purchaseProc.Object, _stripeTransactionProc.Object,
            _paymentService.Object, _taxService.Object, _pricingService.Object,
            _emailService.Object, _settingsService.Object, orgProcMock.Object,
            enrichmentMock.Object);

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
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            VenueId = venue.Id,
            BusinessUserId = _userId
        };
        _context.Events.Add(ev);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyRefunded_ThrowsInvalidOperationException()
    {
        var purchaseId = Guid.NewGuid();
        _context.Purchases.Add(new Purchase
        {
            Id = purchaseId,
            PurchaseNumber = "BK-TEST-999999",
            Status = PurchaseStatus.Refunded,
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 5000,
            FeeCents = 0,
            TotalCents = 5000
        });
        _context.StripeTransactions.Add(new StripeTransaction
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            PaymentIntentId = "pi_test_cancel",
            Status = PaymentStatus.Refunded,
            AmountCents = 5000,
            RefundedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => _service.CancelAsync(purchaseId, _userId);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RefundAsync_WhenPurchaseNotFound_ThrowsKeyNotFoundException()
    {
        var act = () => _service.RefundAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetQrImageAsync_WhenPurchaseNotFound_ThrowsKeyNotFoundException()
    {
        var act = () => _service.GetQrImageAsync(Guid.NewGuid(), _userId);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenPurchaseAlreadyPaid_ReturnsPurchaseDtoIdempotently()
    {
        var purchaseId = Guid.NewGuid();
        var purchaseView = new Db.Entities.Views.PurchaseView
        {
            PurchaseId = purchaseId,
            PurchaseNumber = "BK-TEST-111111",
            Status = "Paid",
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 5000,
            FeeCents = 0,
            TotalCents = 5000,
            UserFirstName = "Test",
            UserLastName = "User",
            UserEmail = "test@example.com",
            EventTitle = "Test Event",
            EventStartDate = DateTime.UtcNow.AddDays(1),
            EventEndDate = DateTime.UtcNow.AddDays(2),
            VenueName = "Test Venue"
        };
        _context.PurchaseViews.Add(purchaseView);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.ConfirmPaymentAsync(purchaseId, _userId);

        result.Should().NotBeNull();
        result.PurchaseId.Should().Be(purchaseId);
        result.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenPurchaseAlreadyCheckedIn_ReturnsPurchaseDtoIdempotently()
    {
        var purchaseId = Guid.NewGuid();
        var purchaseView = new Db.Entities.Views.PurchaseView
        {
            PurchaseId = purchaseId,
            PurchaseNumber = "BK-TEST-222222",
            Status = "CheckedIn",
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 5000,
            FeeCents = 0,
            TotalCents = 5000,
            UserFirstName = "Test",
            UserLastName = "User",
            UserEmail = "test@example.com",
            EventTitle = "Test Event",
            EventStartDate = DateTime.UtcNow.AddDays(1),
            EventEndDate = DateTime.UtcNow.AddDays(2),
            VenueName = "Test Venue"
        };
        _context.PurchaseViews.Add(purchaseView);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.ConfirmPaymentAsync(purchaseId, _userId);

        result.Should().NotBeNull();
        result.PurchaseId.Should().Be(purchaseId);
        result.Status.Should().Be("CheckedIn");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WhenPending_ConfirmsPaymentAndSendsDetailedEmail()
    {
        var purchaseId = Guid.NewGuid();
        var purchaseView = new Db.Entities.Views.PurchaseView
        {
            PurchaseId = purchaseId,
            PurchaseNumber = "BK-TEST-333333",
            Status = "Pending",
            UserId = _userId,
            EventId = _eventId,
            SubtotalCents = 14100,
            FeeCents = 0,
            TotalCents = 14100,
            UserFirstName = "Test",
            UserLastName = "User",
            UserEmail = "test@example.com",
            EventTitle = "Test Event",
            EventStartDate = DateTime.UtcNow.AddDays(1),
            EventEndDate = DateTime.UtcNow.AddDays(2),
            VenueName = "Test Venue",
            PaymentIntentId = "pi_test_123",
            PaymentAmountCents = 14100,
            TicketCount = 2,
            TaxAmountCents = 776,
            TotalChargedCents = 14876
        };
        _context.PurchaseViews.Add(purchaseView);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _paymentService.Setup(p => p.GetPaymentIntentAsync("pi_test_123"))
            .ReturnsAsync(new PaymentIntentDetails("pi_test_123", 14100, 14100, "succeeded"));

        var result = await _service.ConfirmPaymentAsync(purchaseId, _userId);

        result.Should().NotBeNull();
        result.PurchaseId.Should().Be(purchaseId);

        _stripeTransactionProc.Verify(s => s.UpdateStatusAsync("pi_test_123", "Succeeded", It.IsAny<CancellationToken>()), Times.Once);
        _purchaseProc.Verify(p => p.ConfirmPurchaseAsync(purchaseId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        _emailService.Verify(e => e.SendAsync(
            "test@example.com",
            It.Is<string>(s => s.Contains("Purchase Confirmed")),
            It.Is<string>(body => 
                body.Contains("BK-TEST-333333") && 
                body.Contains("Test Event") && 
                body.Contains("2") && 
                body.Contains("$141.00") && 
                body.Contains("$7.76") && 
                body.Contains("$148.76")
            )
        ), Times.Once);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
