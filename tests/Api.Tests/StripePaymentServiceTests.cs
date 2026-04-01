using Api.Services;
using FluentAssertions;
using Moq;

namespace Api.Tests;

public class StripePaymentServiceTests
{
    private readonly Mock<ISettingsService> _settingsService;
    private readonly StripePaymentService _service;

    public StripePaymentServiceTests()
    {
        _settingsService = new Mock<ISettingsService>();
        _service = new StripePaymentService(_settingsService.Object);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_WithMockDevKey_ThrowsInvalidOperationException()
    {
        _settingsService.Setup(s => s.GetAsync("stripe_secret_key")).ReturnsAsync("MOCK_DEV");

        var act = () => _service.CreatePaymentIntentAsync(5000);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WithMockDevKey_ThrowsInvalidOperationException()
    {
        _settingsService.Setup(s => s.GetAsync("stripe_secret_key")).ReturnsAsync("MOCK_DEV");

        var act = () => _service.ConfirmPaymentAsync("pi_test_123");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }
}
