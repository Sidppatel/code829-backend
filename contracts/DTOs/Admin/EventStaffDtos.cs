namespace Contracts.DTOs.Admin;

public record AssignStaffRequest(Guid BusinessUserId);

public record EventStaffDto(
    Guid BusinessUserEventId,
    Guid BusinessUserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime AssignedAt);

public record StaffEventDto(
    Guid EventId,
    string Title,
    string Slug,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string? ImagePath);
