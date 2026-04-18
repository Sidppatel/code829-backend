namespace Contracts.DTOs.Logs;

public record DeveloperLogDto(
    Guid DeveloperLogId,
    DateTime Timestamp,
    string Severity,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? RequestPath,
    string? RequestMethod,
    int? StatusCode,
    Guid? AdminUserId,
    string? IpAddress,
    string? CorrelationId,
    string? MetadataJson
);
