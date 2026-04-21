namespace Contracts.DTOs.Logs;

public record DeveloperLogDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] Guid DeveloperLogId,
    DateTime Timestamp,
    string Severity,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? RequestPath,
    string? RequestMethod,
    int? StatusCode,
    Guid? BusinessUserId,
    string? IpAddress,
    string? CorrelationId,
    string? MetadataJson
);
