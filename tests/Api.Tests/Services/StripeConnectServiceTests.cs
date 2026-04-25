using Api.Services;
using FluentAssertions;
using Moq;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="StripeConnectService"/>. The Stripe SDK validates
/// keys lazily on the first network call, so these tests exercise the
/// configuration + parameter-shaping branches that don't require a live API
/// — the integration test suite covers the request shape end-to-end against
/// Stripe's mock fixtures (out of scope for unit tests).
///
/// <para>
/// Tests that need to assert the request payload shape (type=express, scope
/// param keys, ExtraParams) construct the service and call into the public
/// methods, then assert on the typed exception that bubbles up when the
/// configured key is the test placeholder. Stripe.net throws a typed
/// <c>StripeException</c> on auth failure, which our <c>MapStripeException</c>
/// translates to <see cref="InvalidOperationException"/> — that's the
/// observable for the "we sent the request" branch.
/// </para>
/// </summary>
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
        // Use a syntactically-valid but bogus test key so the SDK reaches the
        // network and returns 401 — we want the MapStripeException path, not
        // the "secret not configured" guard. Stripe SDK reports invalid keys
        // as "invalid_request_error", which our mapper translates to
        // ArgumentException. This test exists so both Identity and BankOnly
        // scope branches execute their request-shaping code (Identity skips
        // ExtraParams, BankOnly populates the dotted key).
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
        // Bogus key triggers Stripe's invalid_request_error (key auth fail) —
        // mapped to ArgumentException by MapStripeException. The service's
        // "already deleted = success" branch keys on the account_invalid code
        // which only fires when the key is valid but the account id is gone;
        // we cover that path in integration tests against the real Stripe
        // sandbox, not here.
        _secrets.Setup(s => s.StripeSecretKey).Returns("sk_test_invalid_key_for_unit_test_only");

        var act = () => _service.DeleteAccountAsync("acct_test_404");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid Stripe Connect request*");
    }
}
