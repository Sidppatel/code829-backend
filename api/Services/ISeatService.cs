using Contracts.DTOs.Seats;

namespace Api.Services;

public interface ISeatService
{
    Task<VenueLayoutDto> GetLayoutAsync(Guid venueId, Guid? eventId = null);
    Task<SeatHoldDto> HoldSeatAsync(Guid userId, Guid eventId, Guid seatId, Guid ticketTypeId);
    Task ReleaseSeatAsync(Guid userId, Guid eventId, Guid seatId);
    Task<List<SeatHoldDto>> GetUserHoldsAsync(Guid userId, Guid eventId);
    Task<List<SeatHoldDto>> HoldTableAsync(Guid userId, Guid eventId, Guid tableId, Guid ticketTypeId);
    Task ReleaseTableAsync(Guid userId, Guid eventId, Guid tableId);
    Task<int> CleanupExpiredHoldsAsync();
}
