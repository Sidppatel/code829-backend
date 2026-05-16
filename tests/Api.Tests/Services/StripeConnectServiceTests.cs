using Api.Services;
using FluentAssertions;
using Moq;

namespace Api.Tests.Services;

public class StripeConnectServiceTests
{
    private readonly Mock<ISecretsProvider> _secrets;
    private readonly StripeConnectService _service;

    private static readonly StripeBusinessProfilePrefill TestPrefill = new(
        LegalName: "Test Org LLC",
        ProductDescription: "Event tickets and admissions sold via the Test platform.",
        Mcc: "7922",
        BusinessType: "individual");

    public StripeConnectServiceTests()
    {
        _secrets = new Mock<ISecretsProvider>();
        _secrets.Setup(s => s.FrontendUrlAdmin).Returns("http://localhost:5174");
        _service = new StripeConnectService(_secrets.Object);
    }

    [Fact]
    public async Task CreateExpressAccountAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.CreateExpressAccountAsync(Guid.NewGuid(), "test@example.com", "US", TestPrefill);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task CreateOnboardingLinkAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.CreateOnboardingLinkAsync("acct_test_123", OnboardingLinkScope.Identity);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task FetchAccountStatusAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.FetchAccountStatusAsync("acct_test_123");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Theory]
    [InlineData(OnboardingLinkScope.Identity)]
    [InlineData(OnboardingLinkScope.BankOnly)]
    public async Task CreateOnboardingLinkAsync_WithBogusKey_TranslatesStripeAuthErrorToTypedException(OnboardingLinkScope scope)
    {

        _secrets.Setup(s => s.StripeSecretKey).Returns("sk_test_invalid_key_for_unit_test_only");

        var act = () => _service.CreateOnboardingLinkAsync("acct_test_404", scope);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid Stripe Connect request*");
    }

    [Fact]
    public async Task CreateExpressAccountAsync_WithBogusKey_TranslatesStripeAuthErrorToTypedException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("sk_test_invalid_key_for_unit_test_only");

        var act = () => _service.CreateExpressAccountAsync(Guid.NewGuid(), "test@example.com", "US", TestPrefill);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid Stripe Connect request*");
    }

    [Fact]
    public async Task FetchAccountStatusAsync_WithBogusKey_TranslatesStripeAuthErrorToTypedException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("sk_test_invalid_key_for_unit_test_only");

        var act = () => _service.FetchAccountStatusAsync("acct_test_404");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid Stripe Connect request*");
    }

    [Fact]
    public async Task CreateLoginLinkAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.CreateLoginLinkAsync("acct_test_123");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task CreateLoginLinkAsync_WithBogusKey_TranslatesStripeAuthErrorToTypedException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("sk_test_invalid_key_for_unit_test_only");
        var act = () => _service.CreateLoginLinkAsync("acct_test_404");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid Stripe Connect request*");
    }

    [Fact]
    public async Task DeleteAccountAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.DeleteAccountAsync("acct_test_123");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task DeleteAccountAsync_WithBogusKey_TranslatesStripeAuthErrorToTypedException()
    {

        _secrets.Setup(s => s.StripeSecretKey).Returns("sk_test_invalid_key_for_unit_test_only");

        var act = () => _service.DeleteAccountAsync("acct_test_404");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid Stripe Connect request*");
    }
}
