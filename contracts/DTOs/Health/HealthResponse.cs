namespace Contracts.DTOs.Health;

public record HealthResponse(
    string Status,
    string Version,
    DateTime Timestamp,
    Dictionary<string, string> Services
);
