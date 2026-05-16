using Contracts.DTOs.Purchases;
using Db;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Services;

public interface IPricingService
{
    Task<PublicQuoteDto> CalculatePublicQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default);
    Task<CheckoutQuoteDto> CalculateCheckoutQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default);
    Task<AdminQuoteDto> CalculateAdminQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default);
    Task<PricingComputation> ComputeForPurchaseAsync(PricingQuoteRequest request, CancellationToken ct = default);
}

public record PricingComputation(
    int SubtotalCents,
    int FeeCents,
    int TaxCents,
    int TotalCents,
    int PaymentIntentAmountCents,
    int SeatsIncluded,
    string? TaxCalculationId,
    string Currency,
    List<QuoteLineDto> Lines
);

public class PricingService(
    EventPlatformDbContext context,
    ITaxService taxService,
    ISettingsService settings
) : IPricingService
{
    public async Task<PublicQuoteDto> CalculatePublicQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default)
    {

        var (subtotal, fee, seats, _, _) = await ComputeBaseAsync(request, ct);
        var displayTotal = subtotal + fee;
        return new PublicQuoteDto(
            displayTotal,
            seats,
            "usd",
            FormatUsd(displayTotal),
            DateTime.UtcNow.AddMinutes(5));
    }

    public async Task<CheckoutQuoteDto> CalculateCheckoutQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default)
    {
        var comp = await ComputeForPurchaseAsync(request, ct);
        var displayTotal = comp.SubtotalCents + comp.FeeCents;
        return new CheckoutQuoteDto(
            displayTotal,
            comp.TaxCents,
            comp.PaymentIntentAmountCents,
            comp.SeatsIncluded,
            comp.Currency,
            FormatUsd(displayTotal),
            FormatUsd(comp.TaxCents),
            FormatUsd(comp.PaymentIntentAmountCents),
            comp.TaxCalculationId,
            DateTime.UtcNow.AddMinutes(5));
    }

    public async Task<AdminQuoteDto> CalculateAdminQuoteAsync(PricingQuoteRequest request, CancellationToken ct = default)
    {
        var comp = await ComputeForPurchaseAsync(request, ct);
        var displayTotal = comp.SubtotalCents + comp.FeeCents;
        return new AdminQuoteDto(
            comp.SubtotalCents,
            comp.FeeCents,
            displayTotal,
            comp.TaxCents,
            comp.PaymentIntentAmountCents,
            comp.SeatsIncluded,
            comp.Currency,
            FormatUsd(displayTotal),
            FormatUsd(comp.PaymentIntentAmountCents),
            comp.TaxCalculationId,
            DateTime.UtcNow.AddMinutes(5),
            comp.Lines);
    }

    public async Task<PricingComputation> ComputeForPurchaseAsync(PricingQuoteRequest request, CancellationToken ct = default)
    {
        var (subtotal, fee, seats, lines, ev) = await ComputeBaseAsync(request, ct);
        return await ApplyTaxIfEnabledAsync(ev, subtotal, fee, subtotal + fee, seats, lines, ct);
    }

    private async Task<(int Subtotal, int Fee, int Seats, List<QuoteLineDto> Lines, EventView Event)> ComputeBaseAsync(
        PricingQuoteRequest request, CancellationToken ct)
    {
        var ev = await context.EventViews.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == request.EventId, ct)
            ?? throw new KeyNotFoundException("Event not found");

        if (request.TableIds is { Count: > 0 })
        {
            if (request.SeatCount.HasValue)
                throw new InvalidOperationException("SeatCount is not valid for table bookings");
            return await ComputeTableBaseAsync(ev, request.TableIds, ct);
        }

        if (request.SeatCount is int seats and > 0)
        {
            if (request.TableIds is { Count: > 0 })
                throw new InvalidOperationException("TableIds is not valid for open-capacity bookings");
            return await ComputeOpenBaseAsync(ev, seats, request.EventTicketTypeId, ct);
        }

        throw new InvalidOperationException("Either TableIds or SeatCount must be provided");
    }

    private async Task<(int, int, int, List<QuoteLineDto>, EventView)> ComputeTableBaseAsync(
        EventView ev, List<Guid> tableIds, CancellationToken ct)
    {
        if (ev.LayoutMode != "Grid")
            throw new InvalidOperationException("Table pricing only applies to Grid events");

        var tables = await context.TableViews.AsNoTracking()
            .Where(t => tableIds.Contains(t.TableId) && t.EventId == ev.EventId)
            .ToListAsync(ct);

        if (tables.Count != tableIds.Count)
            throw new KeyNotFoundException("One or more tables not found for this event");

        var defaultFeeCents = await settings.GetIntAsync("default_platform_fee_grid_cents", 2500);

        var subtotal = tables.Sum(t => t.PriceCents);
        var fee = tables.Sum(t => t.PlatformFeeCents ?? defaultFeeCents);
        var seatsIncluded = tables.Sum(t => t.Capacity);

        var lines = tables.Select(t =>
        {
            var lineFee = t.PlatformFeeCents ?? defaultFeeCents;
            return new QuoteLineDto(
                t.TableId,
                null,
                t.Label,
                1,
                t.PriceCents,
                lineFee,
                t.PriceCents + lineFee);
        }).ToList();

        return (subtotal, fee, seatsIncluded, lines, ev);
    }

    private async Task<(int, int, int, List<QuoteLineDto>, EventView)> ComputeOpenBaseAsync(
        EventView ev, int seats, Guid? ticketTypeId, CancellationToken ct)
    {
        if (ev.LayoutMode != "Open")
            throw new InvalidOperationException("Open pricing only applies to Open events");

        var ticketTypes = await context.EventTicketTypeSummaryViews.AsNoTracking()
            .Where(tt => tt.EventId == ev.EventId && tt.IsActive)
            .ToListAsync(ct);

        int pricePerPerson;
        int feePerTicket;
        Guid? lineTicketTypeId = null;
        string lineLabel;

        if (ticketTypes.Count > 0)
        {
            if (ticketTypeId is null)
                throw new InvalidOperationException("This event requires a ticket type selection");
            var selected = ticketTypes.FirstOrDefault(tt => tt.EventTicketTypeId == ticketTypeId)
                ?? throw new KeyNotFoundException("Ticket type not found or inactive");
            pricePerPerson = selected.PriceCents;
            var defaultOpenFee = await settings.GetIntAsync("default_platform_fee_open_cents", 1000);
            feePerTicket = selected.PlatformFeeCents ?? defaultOpenFee;
            lineTicketTypeId = selected.EventTicketTypeId;
            lineLabel = selected.Label;
        }
        else
        {
            pricePerPerson = ev.PricePerPersonCents
                ?? throw new InvalidOperationException("Event has no price configured");
            feePerTicket = await settings.GetIntAsync("default_platform_fee_open_cents", 1000);
            lineLabel = "General Admission";
        }

        var subtotal = pricePerPerson * seats;
        var fee = feePerTicket * seats;

        var lines = new List<QuoteLineDto>
        {
            new(null, lineTicketTypeId, lineLabel, seats, pricePerPerson, feePerTicket, pricePerPerson + feePerTicket)
        };

        return (subtotal, fee, seats, lines, ev);
    }

    private async Task<PricingComputation> ApplyTaxIfEnabledAsync(
        EventView ev, int subtotal, int fee, int total, int seatsIncluded, List<QuoteLineDto> lines, CancellationToken ct)
    {
        var stripeTaxSetting = await settings.GetOrDefaultAsync("stripe_tax_enabled", "false");
        var stripeTaxEnabled = "true".Equals(stripeTaxSetting, StringComparison.OrdinalIgnoreCase);
        if (!stripeTaxEnabled)
            return new PricingComputation(subtotal, fee, 0, total, total, seatsIncluded, null, "usd", lines);

        try
        {
            var taxResult = await taxService.CalculateAsync(total, "usd",
                ev.VenueAddress, ev.VenueCity, ev.VenueState, ev.VenueZipCode, ct: ct);
            return new PricingComputation(subtotal, fee, taxResult.TaxAmountExclusive, total,
                taxResult.AmountTotal, seatsIncluded, taxResult.CalculationId, "usd", lines);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Pricing] Tax calculation failed for event {EventId}", ev.EventId);
            throw new InvalidOperationException("Unable to calculate tax", ex);
        }
    }

    private static string FormatUsd(int cents) => $"${cents / 100.0:F2}";
}
