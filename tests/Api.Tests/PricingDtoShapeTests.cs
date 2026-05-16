using System.Text.Json;
using System.Text.RegularExpressions;
using Contracts.DTOs.Purchases;
using Xunit;

namespace Api.Tests;

public class PricingDtoShapeTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly Regex BreakdownLeakPattern =
        new(@"subtotal|^fee|fee[A-Z_]|tax", RegexOptions.IgnoreCase);

    [Fact]
    public void PublicQuoteDto_OmitsBreakdownAndTax()
    {
        var dto = new PublicQuoteDto(
            DisplayTotalCents: 26500,
            SeatsIncluded: 8,
            Currency: "usd",
            FormattedDisplayTotal: "$265.00",
            ExpiresAt: DateTime.UtcNow.AddMinutes(5));

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            Assert.False(
                BreakdownLeakPattern.IsMatch(prop.Name),
                $"PublicQuoteDto leaks breakdown field '{prop.Name}'");
        }
    }

    [Fact]
    public void CheckoutQuoteDto_IncludesTaxLine()
    {
        var dto = new CheckoutQuoteDto(
            DisplayTotalCents: 26500,
            TaxCents: 2120,
            GrandTotalCents: 28620,
            SeatsIncluded: 8,
            Currency: "usd",
            FormattedDisplayTotal: "$265.00",
            FormattedTax: "$21.20",
            FormattedGrandTotal: "$286.20",
            TaxCalculationId: "txcalc_123",
            ExpiresAt: DateTime.UtcNow.AddMinutes(5));

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("taxCents", out var taxCents));
        Assert.Equal(2120, taxCents.GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("grandTotalCents", out var grand));
        Assert.Equal(28620, grand.GetInt32());

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var n = prop.Name;
            Assert.False(n.Contains("subtotal", StringComparison.OrdinalIgnoreCase),
                $"CheckoutQuoteDto leaks subtotal via '{n}'");
            Assert.False(
                n.EndsWith("FeeCents", StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith("feeCents", StringComparison.Ordinal),
                $"CheckoutQuoteDto leaks fee via '{n}'");
        }
    }

    [Fact]
    public void AdminQuoteDto_IncludesLinesAndBreakdown()
    {
        var lines = new List<QuoteLineDto>
        {
            new(
                TableId: Guid.NewGuid(),
                EventTicketTypeId: null,
                Label: "Round 8",
                Quantity: 1,
                UnitPriceCents: 24000,
                LineFeeCents: 2500,
                LineDisplayCents: 26500)
        };
        var dto = new AdminQuoteDto(
            SubtotalCents: 24000,
            FeeCents: 2500,
            DisplayTotalCents: 26500,
            TaxCents: 2120,
            GrandTotalCents: 28620,
            SeatsIncluded: 8,
            Currency: "usd",
            FormattedDisplayTotal: "$265.00",
            FormattedGrandTotal: "$286.20",
            TaxCalculationId: "txcalc_123",
            ExpiresAt: DateTime.UtcNow.AddMinutes(5),
            Lines: lines);

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("subtotalCents", out _));
        Assert.True(doc.RootElement.TryGetProperty("feeCents", out _));
        Assert.True(doc.RootElement.TryGetProperty("lines", out var linesEl));
        Assert.Equal(JsonValueKind.Array, linesEl.ValueKind);
        Assert.Equal(1, linesEl.GetArrayLength());
        Assert.Equal("Round 8", linesEl[0].GetProperty("label").GetString());
    }
}
