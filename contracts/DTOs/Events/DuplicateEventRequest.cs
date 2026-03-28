namespace Contracts.DTOs.Events;

public record DuplicateEventRequest(
    DateTime StartDate,
    DateTime EndDate
);
