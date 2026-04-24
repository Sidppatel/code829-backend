using Contracts.Enums;
using Db.Repositories.StoredProcedures;

namespace Api.Services;

/// <summary>
/// Legacy wrapper kept for 1-release deprecation. Dual-writes to both the unified
/// audit_logs table (via IAuditLogService) and the legacy business_logs table so
/// existing read SPs (sp_get_admin_logs) keep returning data until the follow-up
/// migration drops the legacy tables + read path switches to audit_logs.
/// </summary>
public class AdminLogService(IAuditLogService audit, ILogProcedures logProc) : IAdminLogService
{
    public async Task LogAsync(string action, string? entityType, Guid? entityId, string description,
        Guid? adminUserId = null, string? metadataJson = null, string? ipAddress = null)
    {
        await audit.LogAsync(
            eventType: action,
            actorType: AuditActorType.Admin,
            actorId: adminUserId,
            subjectType: entityType,
            subjectId: entityId,
            action: action,
            metadataJson: metadataJson,
            ip: ipAddress);

        await logProc.CreateAdminLogAsync(action, adminUserId,
            entityType, entityId, description, metadataJson, ipAddress);
    }
}
