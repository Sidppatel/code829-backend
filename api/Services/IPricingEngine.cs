namespace Api.Services;

public interface IPricingEngine
{
    Task<(int SubtotalCents, int FeeCents, int TotalCents)> CalculateAsync(Guid eventId, List<int> itemPricesCents);
}
