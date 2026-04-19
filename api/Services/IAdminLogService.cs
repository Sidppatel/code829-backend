namespace Api.Services;

public interface IAdminLogService
{
    Task LogAsync(string action, string? entityType, Guid? entityId, string description,
        Guid? adminUserId = null, string? metadataJson = null, string? ipAddress = null);
}
