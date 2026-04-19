using Db.Repositories.StoredProcedures;

namespace Api.Services;

public class AdminLogService(ILogProcedures logProc) : IAdminLogService
{
    public async Task LogAsync(string action, string? entityType, Guid? entityId, string description,
        Guid? adminUserId = null, string? metadataJson = null, string? ipAddress = null)
    {
        await logProc.CreateAdminLogAsync(action, adminUserId,
            entityType, entityId, description, metadataJson, ipAddress);
    }
}
