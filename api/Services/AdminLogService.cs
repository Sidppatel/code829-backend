using Db;
using Db.Entities;

namespace Api.Services;

public class AdminLogService(EventPlatformDbContext context) : IAdminLogService
{
    public async Task LogAsync(string action, string? entityType, Guid? entityId, string description,
        Guid? actorId = null, string? actorEmail = null, string? actorRole = null,
        string? metadataJson = null, string? ipAddress = null)
    {
        context.AdminLogs.Add(new AdminLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            MetadataJson = metadataJson,
            IpAddress = ipAddress
        });
        await context.SaveChangesAsync();
    }
}
