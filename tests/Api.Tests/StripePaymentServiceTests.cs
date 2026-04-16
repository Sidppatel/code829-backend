using Api.Services;
using FluentAssertions;
using Moq;

namespace Api.Tests;

public class StripePaymentServiceTests
{
    private readonly Mock<ISecretsProvider> _secrets;
    private readonly StripePaymentService _service;

    public StripePaymentServiceTests()
    {
        _secrets = new Mock<ISecretsProvider>();
        _service = new StripePaymentService(_secrets.Object);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.CreatePaymentIntentAsync(5000, 1500, null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WithEmptyKey_ThrowsInvalidOperationException()
    {
        _secrets.Setup(s => s.StripeSecretKey).Returns("");

        var act = () => _service.ConfirmPaymentAsync("pi_test_123");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }
}
