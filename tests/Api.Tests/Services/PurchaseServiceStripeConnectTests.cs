using Api.Exceptions;
using Api.Services;
using Contracts.DTOs.Purchases;
using Contracts.Enums;
using Db;
using Db.Entities;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using FluentAssertions;
using Moq;

namespace Api.Tests.Services;

/// <summary>
/// Connect-enforcement-specific PurchaseService tests. Verifies the new
/// guard added in <c>PurchaseService.EnsurePayoutReadyIfEnforcedAsync</c>
/// behaves correctly under all four combinations of (flag-enabled,
/// org-state).
///
/// <para>
/// Uses the InMemory DbContext from <see cref="TestDbContextFactory"/>;
/// EventViews is seeded directly because InMemory treats ToView entries the
/// same as ToTable entries — perfectly fine for this scope where we don't
/// exercise the real PG view definition.
/// </para>
/// </summary>
public class PurchaseServiceStripeConnectTests : IDisposable
{
    private readonly EventPlatformDbContext _context;
    private readonly Mock<IPurchaseProcedures> _purchaseProc;
    private readonly Mock<IStripeTransactionProcedures> _stripeTransactionProc;
    private readonly Mock<IPaymentService> _paymentService;
    private readonly Mock<ITaxService> _taxService;
    private readonly Mock<IPricingService> _pricingService;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<ISettingsService> _settingsService;
    private readonly Mock<IOrganizationProcedures> _organizationProc;
    private readonly PurchaseService _service;
    private readonly Guid _userId;
    private readonly Guid _eventId;
    private readonly Guid _businessUserId;

    public PurchaseServiceStripeConnectTests()
    {
        _context = TestDbContextFactory.Create();

        _purchaseProc = new Mock<IPurchaseProcedures>();
        _stripeTransactionProc = new Mock<IStripeTransactionProcedures>();
        _paymentService = new Mock<IPaymentService>();
        _taxService = new Mock<ITaxService>();
        _pricingService = new Mock<IPricingService>();
        _emailService = new Mock<IEmailService>();
        _settingsService = new Mock<ISettingsService>();
        _organizationProc = new Mock<IOrganizationProcedures>();

        _userId = Guid.NewGuid();
        _eventId = Guid.NewGuid();
        _businessUserId = Guid.NewGuid();

        var enrichmentMock = new Mock<IPaymentEnrichmentService>();
        _service = new PurchaseService(_context,
            _purchaseProc.Object, _stripeTransactionProc.Object,
            _paymentService.Object, _taxService.Object, _pricingService.Object,
            _emailService.Object, _settingsService.Object, _organizationProc.Object,
            enrichmentMock.Object);

        SeedEvent();
    }

    private void SeedEvent()
    {
        // Direct INSERT into the EventViews "table" (InMemory provider treats
        // ToView the same as ToTable). The real Postgres view is a SELECT —
        // this seeding is a unit-test shortcut to avoid wiring up base tables
        // for the read view.
        _context.EventViews.Add(new EventView
        {
            EventId = _eventId,
            Title = "Spring Gala",
            Slug = "spring-gala",
            Status = "Published",
            Category = "Concert",
            StartDate = DateTime.UtcNow.AddDays(7),
            EndDate = DateTime.UtcNow.AddDays(7).AddHours(4),
            LayoutMode = "Open",
            MaxCapacity = 100,
            PricePerPersonCents = 5000,
            VenueId = Guid.NewGuid(),
            BusinessUserId = _businessUserId,
            VenueName = "Lyric Hall",
            VenueAddress = "1 Test St",
            VenueCity = "Townsville",
            VenueState = "ST",
            VenueZipCode = "00000",
            OrganizerFirstName = "Org",
            OrganizerLastName = "Anizer"
        });
        _context.SaveChanges();
    }

    private void SetEnforcement(bool enabled)
    {
        _settingsService.Setup(s => s.GetOrDefaultAsync(SettingsKeys.ConnectEnforcementEnabled, It.IsAny<string?>()))
            .ReturnsAsync(enabled ? "true" : "false");
        // Pricing service uses default platform fee from settings; supply a
        // safe default for any other key the service might read.
        _settingsService.Setup(s => s.GetOrDefaultAsync(
                It.Is<string>(k => k != SettingsKeys.ConnectEnforcementEnabled), It.IsAny<string?>()))
            .ReturnsAsync((string _, string? def) => def ?? "10");
    }

    private void SetupPricing()
    {
        // Minimal pricing breakdown so the call survives long enough to reach
        // the enforcement guard. SubtotalCents must be >0 so the metadata
        // builder produces a sensible payload to assert on.
        _pricingService.Setup(p => p.ComputeForPurchaseAsync(It.IsAny<PricingQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingComputation(
                SubtotalCents: 5000,
                FeeCents: 100,
                TaxCents: 50,
                TotalCents: 5150,
                PaymentIntentAmountCents: 5150,
                SeatsIncluded: 1,
                TaxCalculationId: "txcalc_test_123",
                Currency: "usd",
                Lines: []));
    }

    private static CreatePurchaseRequest OpenSeatRequest(Guid eventId)
        => new(EventId: eventId, TableId: null, TableIds: null, SeatsReserved: 1, EventTicketTypeId: null);

    // ─── Enforcement = false (rollout flag off) — purchase succeeds regardless ─

    [Theory]
    [InlineData(false, null)] // BU has no org
    [InlineData(false, "")]   // org has no Stripe acct
    [InlineData(false, "acct_pending")] // org has acct but unverified
    public async Task CreateAsync_EnforcementDisabled_AllowsPurchaseRegardlessOfOrgState(
        bool enforced, string? acctId)
    {
        SetEnforcement(enforced);
        SetupPricing();

        if (acctId is null)
        {
            _organizationProc.Setup(p => p.GetByBusinessUserAsync(_businessUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Organization?)null);
        }
        else
        {
            _organizationProc.Setup(p => p.GetByBusinessUserAsync(_businessUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Organization
                {
                    Id = Guid.NewGuid(),
                    Name = "Org",
                    CountryCode = "US",
                    StripeConnectedAccountId = string.IsNullOrEmpty(acctId) ? null : acctId,
                    StripeChargesEnabled = false
                });
        }

        _paymentService.Setup(p => p.CreatePaymentIntentAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync(("pi_test_xx", "pi_test_xx_secret_yy", "requires_payment_method"));

        _purchaseProc.Setup(p => p.ReserveOpenCapacityAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var act = () => _service.CreateAsync(_userId, OpenSeatRequest(_eventId));

        // Either succeeds (when downstream GetByIdAsync finds nothing → null
        // → InvalidOperationException("Purchase creation failed")) or throws
        // a generic "Purchase creation failed" — the key assertion is that
        // OrganizationNotPayoutReadyException is NEVER thrown.
        await act.Should().NotThrowAsync<OrganizationNotPayoutReadyException>();
    }

    // ─── Enforcement = true ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_EnforcementEnabled_RejectsWhenBusinessUserHasNoOrganization()
    {
        SetEnforcement(true);
        SetupPricing();
        _organizationProc.Setup(p => p.GetByBusinessUserAsync(_businessUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        var act = () => _service.CreateAsync(_userId, OpenSeatRequest(_eventId));

        await act.Should().ThrowAsync<OrganizationNotPayoutReadyException>()
            .WithMessage("*organization*");
    }

    [Fact]
    public async Task CreateAsync_EnforcementEnabled_RejectsWhenOrganizationHasNoStripeAccount()
    {
        SetEnforcement(true);
        SetupPricing();
        _organizationProc.Setup(p => p.GetByBusinessUserAsync(_businessUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Org",
                CountryCode = "US",
                StripeConnectedAccountId = null,
                StripeChargesEnabled = false
            });

        var act = () => _service.CreateAsync(_userId, OpenSeatRequest(_eventId));

        await act.Should().ThrowAsync<OrganizationNotPayoutReadyException>()
            .WithMessage("*payouts*");
    }

    [Fact]
    public async Task CreateAsync_EnforcementEnabled_RejectsWhenChargesDisabled()
    {
        SetEnforcement(true);
        SetupPricing();
        _organizationProc.Setup(p => p.GetByBusinessUserAsync(_businessUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Org",
                CountryCode = "US",
                StripeConnectedAccountId = "acct_pending",
                StripeChargesEnabled = false
            });

        var act = () => _service.CreateAsync(_userId, OpenSeatRequest(_eventId));

        await act.Should().ThrowAsync<OrganizationNotPayoutReadyException>()
            .WithMessage("*payouts*");
    }

    // ─── Metadata payload — ensure Connect-relevant keys are sent through to Stripe ─

    [Fact]
    public async Task CreateAsync_PassesAllRequiredMetadataKeysToPaymentIntent()
    {
        SetEnforcement(true);
        SetupPricing();
        _organizationProc.Setup(p => p.GetByBusinessUserAsync(_businessUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Org",
                CountryCode = "US",
                StripeConnectedAccountId = "acct_active",
                StripeChargesEnabled = true,
                StripePayoutsEnabled = true
            });

        IDictionary<string, string>? capturedMetadata = null;
        _paymentService.Setup(p => p.CreatePaymentIntentAsync(
                It.IsAny<int>(), It.IsAny<int>(), "acct_active", It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<int, int, string?, string, IDictionary<string, string>?, string?, string?>(
                (_, _, _, _, metadata, _, _) => capturedMetadata = metadata)
            .ReturnsAsync(("pi_meta_test", "pi_meta_test_secret", "requires_payment_method"));

        _purchaseProc.Setup(p => p.ReserveOpenCapacityAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // The downstream GetByIdAsync hits PurchaseViews which we haven't
        // seeded — accept the resulting throw, the metadata capture happens
        // before that branch.
        try { await _service.CreateAsync(_userId, OpenSeatRequest(_eventId)); }
        catch (InvalidOperationException) { /* expected — no PurchaseView seed */ }

        capturedMetadata.Should().NotBeNull();
        capturedMetadata!.Should().ContainKey("purchase_number");
        capturedMetadata.Should().ContainKey("event_id");
        capturedMetadata.Should().ContainKey("event_name");
        capturedMetadata.Should().ContainKey("event_type");
        capturedMetadata.Should().ContainKey("event_start_date");
        capturedMetadata.Should().ContainKey("subtotal_cents");
        capturedMetadata.Should().ContainKey("platform_fee_cents");
        capturedMetadata.Should().ContainKey("tax_cents");
        capturedMetadata.Should().ContainKey("total_cents");
        capturedMetadata.Should().ContainKey("admin_payout_cents");
        capturedMetadata.Should().ContainKey("developer_gross_cents");
        capturedMetadata.Should().ContainKey("tax_calculation");

        capturedMetadata["event_id"].Should().Be(_eventId.ToString());
        capturedMetadata["event_type"].Should().Be("Open");
        capturedMetadata["subtotal_cents"].Should().Be("5000");
        capturedMetadata["platform_fee_cents"].Should().Be("100");
        capturedMetadata["tax_cents"].Should().Be("50");
        capturedMetadata["admin_payout_cents"].Should().Be("5000");
        // developer_gross_cents = platform_fee + tax (per BuildPaymentIntentMetadata XML doc)
        capturedMetadata["developer_gross_cents"].Should().Be("150");

        // No PII permitted on Stripe metadata per project security rule.
        capturedMetadata.Should().NotContainKey("customer_email");
        capturedMetadata.Should().NotContainKey("organizer_email");
        capturedMetadata.Should().NotContainKey("organizer_name");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
