using Api.Services;
using Contracts.DTOs.Organizations;
using Db;
using Db.Entities;
using Db.Repositories.StoredProcedures;
using FluentAssertions;
using Moq;

namespace Api.Tests.Services;

public class OrganizationServiceTests : IDisposable
{
    private readonly EventPlatformDbContext _context;
    private readonly Mock<IOrganizationProcedures> _orgProc;
    private readonly Mock<IBusinessUserProcedures> _businessUserProc;
    private readonly Mock<IStripeConnectService> _stripeConnect;
    private readonly Mock<IEmailService> _emailService;
    private readonly Mock<ISettingsService> _settings;
    private readonly OrganizationService _service;

    public OrganizationServiceTests()
    {
        _context = TestDbContextFactory.Create();
        _orgProc = new Mock<IOrganizationProcedures>();
        _businessUserProc = new Mock<IBusinessUserProcedures>();
        _stripeConnect = new Mock<IStripeConnectService>();
        _emailService = new Mock<IEmailService>();
        _settings = new Mock<ISettingsService>();

        _settings.Setup(s => s.GetOrDefaultAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string? def, CancellationToken _) => def);

        _service = new OrganizationService(
            _context,
            _orgProc.Object, _businessUserProc.Object,
            _stripeConnect.Object, _emailService.Object, _settings.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_WithoutInitialMember_OnlyCallsCreate()
    {
        var newId = Guid.NewGuid();
        _orgProc.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newId);

        var id = await _service.CreateAsync("The Lyric", "Lyric Theatre LLC", "US", null);

        id.Should().Be(newId);
        _orgProc.Verify(p => p.CreateAsync("The Lyric", "Lyric Theatre LLC", "US", It.IsAny<CancellationToken>()), Times.Once);
        _orgProc.Verify(p => p.AddBusinessUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithInitialMember_CreatesAndAttaches()
    {
        var newOrgId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _orgProc.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newOrgId);

        var id = await _service.CreateAsync("Acme Hall", null, "US", memberId);

        id.Should().Be(newOrgId);
        _orgProc.Verify(p => p.AddBusinessUserAsync(memberId, newOrgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_ReturnsMappedDto()
    {
        var orgId = Guid.NewGuid();
        _orgProc.Setup(p => p.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = orgId,
                Name = "The Lyric",
                LegalName = "Lyric LLC",
                CountryCode = "US",
                StripeConnectedAccountId = "acct_123",
                StripeChargesEnabled = true
            });

        var result = await _service.GetAsync(orgId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(orgId);
        result.Name.Should().Be("The Lyric");
        result.StripeConnectedAccountId.Should().Be("acct_123");
        result.StripeChargesEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WhenMissing_ReturnsNull()
    {
        _orgProc.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        var result = await _service.GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PassesAllFieldsToProc()
    {
        var orgId = Guid.NewGuid();
        var req = new OrganizationUpdateRequest("Renamed", "Renamed Inc", "GB");

        await _service.UpdateAsync(orgId, req);

        _orgProc.Verify(p => p.UpdateAsync(orgId, "Renamed", "Renamed Inc", "GB", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_DelegatesToProc()
    {
        var orgId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await _service.AddMemberAsync(orgId, memberId);

        _orgProc.Verify(p => p.AddBusinessUserAsync(memberId, orgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_DelegatesToProc()
    {
        var orgId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await _service.RemoveMemberAsync(orgId, memberId);

        _orgProc.Verify(p => p.RemoveBusinessUserAsync(memberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenSpRaisesFkViolation_PropagatesException()
    {

        _orgProc.Setup(p => p.RemoveBusinessUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Npgsql.PostgresException("Cannot remove last member of org with active Stripe account",
                "ERROR", "ERROR", "23503"));

        var act = () => _service.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<Npgsql.PostgresException>()
            .Where(e => e.SqlState == "23503");
    }

    [Fact]
    public async Task GetByBusinessUserIdAsync_ReturnsNull_WhenBusinessUserHasNoOrganization()
    {
        var buId = Guid.NewGuid();
        _orgProc.Setup(p => p.GetByBusinessUserAsync(buId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        var result = await _service.GetByBusinessUserIdAsync(buId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByBusinessUserIdAsync_ReturnsDto_WhenBusinessUserHasOrganization()
    {
        var buId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        _orgProc.Setup(p => p.GetByBusinessUserAsync(buId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = orgId,
                Name = "Acme",
                CountryCode = "US",
                StripeConnectedAccountId = "acct_xyz"
            });

        var result = await _service.GetByBusinessUserIdAsync(buId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(orgId);
        result.StripeConnectedAccountId.Should().Be("acct_xyz");
    }

    [Fact]
    public async Task ListAsync_ClampsPageSize_ToServerMax()
    {

        var result = await _service.ListAsync(null, page: 1, pageSize: 5000);

        result.PageSize.Should().Be(100);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public async Task ListAsync_ClampsPage_ToMinimumOne(int requestedPage, int expectedPage)
    {
        var result = await _service.ListAsync(null, page: requestedPage, pageSize: 25);

        result.Page.Should().Be(expectedPage);
    }

    private static BusinessUser MakeBu(string firstName = "Admin") => new()
    {
        Id = Guid.NewGuid(),
        Email = "admin@example.com",
        EmailHash = "hash",
        FirstName = firstName,
        LastName = "Person",
        PasswordHash = "bcrypt-test-hash"
    };

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_WhenNeitherIdNorEmailProvided_ThrowsInvalidOp()
    {
        var act = () => _service.SendOnboardingLinkEmailAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*businessUserId or recipientEmail*");
    }

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_WhenOrgMissing_ThrowsKeyNotFound()
    {
        _orgProc.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        var act = () => _service.SendOnboardingLinkEmailAsync(Guid.NewGuid(), businessUserId: Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_WhenBusinessUserMissing_ThrowsKeyNotFound()
    {
        var orgId = Guid.NewGuid();
        _orgProc.Setup(p => p.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = orgId, Name = "Org", CountryCode = "US", StripeConnectedAccountId = "acct_x" });
        _businessUserProc.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessUser?)null);

        var act = () => _service.SendOnboardingLinkEmailAsync(orgId, businessUserId: Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_WhenBusinessUserInDifferentOrg_ThrowsInvalidOp()
    {
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var bu = MakeBu();
        _orgProc.Setup(p => p.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = orgId, Name = "Org", CountryCode = "US", StripeConnectedAccountId = "acct_x" });
        _businessUserProc.Setup(p => p.GetByIdAsync(bu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bu);
        _orgProc.Setup(p => p.GetByBusinessUserAsync(bu.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = otherOrgId, Name = "Other", CountryCode = "US" });

        var act = () => _service.SendOnboardingLinkEmailAsync(orgId, businessUserId: bu.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a member*");
    }

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_WhenOrgHasNoStripeAccount_ThrowsInvalidOp()
    {
        var orgId = Guid.NewGuid();
        _orgProc.Setup(p => p.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = orgId,
                Name = "Org",
                CountryCode = "US",
                StripeConnectedAccountId = null
            });

        var act = () => _service.SendOnboardingLinkEmailAsync(orgId, businessUserId: Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no Stripe account*");
    }

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_HappyPath_BusinessUser_CallsStripeAndEmail()
    {
        var orgId = Guid.NewGuid();
        var bu = MakeBu(firstName: "Alex");
        var org = new Organization { Id = orgId, Name = "The Lyric", CountryCode = "US", StripeConnectedAccountId = "acct_real" };
        _orgProc.Setup(p => p.GetByIdAsync(orgId, It.IsAny<CancellationToken>())).ReturnsAsync(org);
        _businessUserProc.Setup(p => p.GetByIdAsync(bu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bu);
        _orgProc.Setup(p => p.GetByBusinessUserAsync(bu.Id, It.IsAny<CancellationToken>())).ReturnsAsync(org);
        _stripeConnect.Setup(s => s.CreateOnboardingLinkAsync("acct_real", OnboardingLinkScope.Identity))
            .ReturnsAsync("https://connect.stripe.com/onboard/abc");

        await _service.SendOnboardingLinkEmailAsync(orgId, businessUserId: bu.Id);

        _stripeConnect.Verify(s => s.CreateOnboardingLinkAsync("acct_real", OnboardingLinkScope.Identity), Times.Once);
        _emailService.Verify(e => e.SendAsync(
            "admin@example.com",
            It.Is<string>(subj => subj.Contains("The Lyric")),
            It.Is<string>(body => body.Contains("https://connect.stripe.com/onboard/abc"))), Times.Once);
    }

    [Fact]
    public async Task SendOnboardingLinkEmailAsync_HappyPath_RecipientEmailOverride_SendsToOverride()
    {
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "The Lyric", CountryCode = "US", StripeConnectedAccountId = "acct_real" };
        _orgProc.Setup(p => p.GetByIdAsync(orgId, It.IsAny<CancellationToken>())).ReturnsAsync(org);
        _stripeConnect.Setup(s => s.CreateOnboardingLinkAsync("acct_real", OnboardingLinkScope.Identity))
            .ReturnsAsync("https://connect.stripe.com/onboard/abc");

        var result = await _service.SendOnboardingLinkEmailAsync(orgId, recipientEmail: "organizer@third-party.com");

        result.RecipientEmail.Should().Be("organizer@third-party.com");
        _emailService.Verify(e => e.SendAsync(
            "organizer@third-party.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }
}
