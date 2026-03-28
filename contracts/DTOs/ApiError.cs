namespace Contracts.DTOs;

public record ApiError(
    int StatusCode,
    string Message,
    string? Detail = null,
    string? CorrelationId = null
);
