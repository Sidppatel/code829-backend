namespace Api.Services;

public interface IAdminLogService
{
    Task LogAsync(string action, string? entityType, Guid? entityId, string description,
        Guid? actorId = null, string? actorEmail = null, string? actorRole = null,
        string? metadataJson = null, string? ipAddress = null);
}
