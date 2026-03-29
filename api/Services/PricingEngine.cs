using Db;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Calculates booking totals with platform fees.
/// Checks for per-event PricingRule overrides, falls back to global DB settings.
/// All calculations in cents to avoid floating-point issues.
/// </summary>
public class PricingEngine(
    EventPlatformDbContext context,
    ISettingsService settings
) : IPricingEngine
{
    public async Task<(int SubtotalCents, int FeeCents, int TotalCents)> CalculateAsync(
        Guid eventId, List<(int BasePrice, int Fee)> items)
    {
        var subtotal = items.Sum(i => i.BasePrice);
        var granularFee = items.Sum(i => i.Fee);

        // If all items have 0 granular fee, fallback to global percentage/flat fee logic
        if (granularFee > 0)
        {
            return (subtotal, granularFee, subtotal + granularFee);
        }

        // Check for event-specific pricing rule override
        var rule = await context.PricingRules
            .FirstOrDefaultAsync(r => r.EventId == eventId);

        int feePercent;
        int feeFlatCents;

        if (rule is not null)
        {
            feePercent = rule.FeePercent ?? int.Parse(
                await settings.GetOrDefaultAsync("platform_fee_percent", "8") ?? "8");
            feeFlatCents = rule.FeeFlatCents ?? int.Parse(
                await settings.GetOrDefaultAsync("platform_fee_flat_cents", "0") ?? "0");
        }
        else
        {
            feePercent = int.Parse(
                await settings.GetOrDefaultAsync("platform_fee_percent", "8") ?? "8");
            feeFlatCents = int.Parse(
                await settings.GetOrDefaultAsync("platform_fee_flat_cents", "0") ?? "0");
        }

        var fee = (int)Math.Ceiling(subtotal * feePercent / 100.0) + (feeFlatCents * items.Count);
        var total = subtotal + fee;

        return (subtotal, fee, total);
    }
}
