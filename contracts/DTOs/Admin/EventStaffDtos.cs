namespace Contracts.DTOs.Admin;

public record AssignStaffRequest(Guid AdminUserId);

public record EventStaffDto(
    Guid AdminUserEventId,
    Guid AdminUserId,
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
