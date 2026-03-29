namespace Contracts.DTOs.Seats;

public record HoldTableRequest(Guid EventId, Guid TableId, Guid TicketTypeId);
public record ReleaseTableRequest(Guid EventId, Guid TableId);
