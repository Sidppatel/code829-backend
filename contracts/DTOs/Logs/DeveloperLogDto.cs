namespace Contracts.DTOs.Logs;

public record DeveloperLogDto(
    Guid Id,
    DateTime Timestamp,
    string Severity,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? RequestPath,
    string? RequestMethod,
    int? StatusCode,
    Guid? UserId,
    string? IpAddress,
    string? CorrelationId,
    string? MetadataJson
);
