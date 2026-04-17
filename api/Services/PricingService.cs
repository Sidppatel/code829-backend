using Contracts.DTOs.Bookings;
using Db;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

public interface IPricingService
{
    Task<PricingQuoteDto> CalculateQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default);
    Task<PricingComputation> ComputeForBookingAsync(PricingQuoteRequest request, CancellationToken ct = default);
}

/// <summary>
/// Full pricing breakdown used during booking creation. Unlike the quote DTO, this carries
/// context (tax calculation id, final PaymentIntent amount) needed server-side.
/// </summary>
public record PricingComputation(
    int SubtotalCents,
    int FeeCents,
    int TaxCents,
    int TotalCents,
    int PaymentIntentAmountCents,
    string? TaxCalculationId,
    string Currency
);

public class PricingService(
    EventPlatformDbContext context,
    ITaxService taxService,
    ISettingsService settings
) : IPricingService
{
    public async Task<PricingQuoteDto> CalculateQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default)
    {
        var comp = await ComputeForBookingAsync(request, ct);
        return new PricingQuoteDto(
            comp.SubtotalCents,
            comp.FeeCents,
            comp.TaxCents,
            comp.TotalCents,
            comp.Currency,
            FormatUsd(comp.TotalCents),
            DateTime.UtcNow.AddMinutes(5));
    }

    public async Task<PricingComputation> ComputeForBookingAsync(PricingQuoteRequest request, CancellationToken ct = default)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw new KeyNotFoundException("Event not found");

        if (request.TableIds is { Count: > 0 })
            return await ComputeTablePricingAsync(ev, request.TableIds, ct);

        if (request.SeatCount is int seats and > 0)
            return await ComputeOpenPricingAsync(ev, seats, request.EventTicketTypeId, ct);

        throw new InvalidOperationException("Either TableIds or SeatCount must be provided");
    }

    private async Task<PricingComputation> ComputeTablePricingAsync(EventView ev, List<Guid> tableIds, CancellationToken ct)
    {
        if (ev.LayoutMode != "Grid")
            throw new InvalidOperationException("Table pricing only applies to Grid events");

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => tableIds.Contains(t.Id) && t.EventId == ev.Id)
            .ToListAsync(ct);

        if (tables.Count != tableIds.Count)
            throw new KeyNotFoundException("One or more tables not found for this event");

        var defaultFeeCents = await settings.GetIntAsync("default_platform_fee_grid_cents", 2500);

        var subtotal = tables.Sum(t => t.PriceCents);
        var fee = tables.Sum(t => t.PlatformFeeCents ?? defaultFeeCents);
        var total = subtotal + fee;

        return await ApplyTaxIfEnabledAsync(ev, subtotal, fee, total, ct);
    }

    private async Task<PricingComputation> ComputeOpenPricingAsync(EventView ev, int seats, Guid? ticketTypeId, CancellationToken ct)
    {
        if (ev.LayoutMode != "Open")
            throw new InvalidOperationException("Open pricing only applies to Open events");

        var ticketTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == ev.Id && tt.IsActive)
            .ToListAsync(ct);

        int pricePerPerson;
        int feePerTicket;

        if (ticketTypes.Count > 0)
        {
            if (ticketTypeId is null)
                throw new InvalidOperationException("This event requires a ticket type selection");
            var selected = ticketTypes.FirstOrDefault(tt => tt.Id == ticketTypeId)
                ?? throw new KeyNotFoundException("Ticket type not found or inactive");
            pricePerPerson = selected.PriceCents;
            var defaultOpenFee = await settings.GetIntAsync("default_platform_fee_open_cents", 1000);
            feePerTicket = selected.PlatformFeeCents ?? defaultOpenFee;
        }
        else
        {
            pricePerPerson = ev.PricePerPersonCents
                ?? throw new InvalidOperationException("Event has no price configured");
            feePerTicket = await settings.GetIntAsync("default_platform_fee_open_cents", 1000);
        }

        var subtotal = pricePerPerson * seats;
        var fee = feePerTicket * seats;
        var total = subtotal + fee;

        return await ApplyTaxIfEnabledAsync(ev, subtotal, fee, total, ct);
    }

    private async Task<PricingComputation> ApplyTaxIfEnabledAsync(EventView ev, int subtotal, int fee, int total, CancellationToken ct)
    {
        var stripeTaxSetting = await settings.GetOrDefaultAsync("stripe_tax_enabled", "false");
        var stripeTaxEnabled = "true".Equals(stripeTaxSetting, StringComparison.OrdinalIgnoreCase);
        if (!stripeTaxEnabled)
            return new PricingComputation(subtotal, fee, 0, total, total, null, "usd");

        try
        {
            var taxResult = await taxService.CalculateAsync(total, "usd",
                ev.VenueAddress, ev.VenueCity, ev.VenueState, ev.VenueZipCode, ct: ct);
            return new PricingComputation(subtotal, fee, taxResult.TaxAmountExclusive, total,
                taxResult.AmountTotal, taxResult.CalculationId, "usd");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Pricing] Tax calculation failed for event {EventId}", ev.Id);
            throw new InvalidOperationException("Unable to calculate tax", ex);
        }
    }

    private static string FormatUsd(int cents) => $"${cents / 100.0:F2}";
}
