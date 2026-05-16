using Api.Services;
using FluentAssertions;
using Moq;

namespace Api.Tests;

public class SettingsExtensionsTests
{
    private readonly Mock<ISettingsService> _settings = new();

    [Fact]
    public async Task ValidInteger_Returned()
    {
        _settings.Setup(s => s.GetOrDefaultAsync("fee", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync("1500");

        var result = await _settings.Object.GetIntAsync("fee", 2500);

        result.Should().Be(1500);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("12.5")]
    public async Task NonNumeric_FallsBackToDefault(string raw)
    {
        _settings.Setup(s => s.GetOrDefaultAsync("fee", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(raw);

        var result = await _settings.Object.GetIntAsync("fee", 2500);

        result.Should().Be(2500);
    }

    [Fact]
    public async Task NegativeValue_FallsBackToDefault()
    {
        _settings.Setup(s => s.GetOrDefaultAsync("fee", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync("-5");

        var result = await _settings.Object.GetIntAsync("fee", 2500);

        result.Should().Be(2500);
    }

    [Fact]
    public async Task AboveMax_FallsBackToDefault()
    {
        _settings.Setup(s => s.GetOrDefaultAsync("fee", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync("99999999");

        var result = await _settings.Object.GetIntAsync("fee", 2500);

        result.Should().Be(2500);
    }

    [Fact]
    public async Task CustomRange_AppliesBothBounds()
    {
        _settings.Setup(s => s.GetOrDefaultAsync("k", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync("50");

        var tooHigh = await _settings.Object.GetIntAsync("k", 10, min: 0, max: 20);
        tooHigh.Should().Be(10);

        _settings.Setup(s => s.GetOrDefaultAsync("k", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync("15");
        var inRange = await _settings.Object.GetIntAsync("k", 10, min: 0, max: 20);
        inRange.Should().Be(15);
    }

    [Fact]
    public async Task NullValue_FallsBackToDefault()
    {
        _settings.Setup(s => s.GetOrDefaultAsync("fee", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var result = await _settings.Object.GetIntAsync("fee", 2500);

        result.Should().Be(2500);
    }
}
