using Db.Entities;

namespace Db.Repositories;

/// <summary>
/// Email-log persistence only. Admin/developer/system audit trails go through
/// IAuditLogService → audit_logs. Legacy methods were removed in the
/// DropLegacyLogTables migration.
/// </summary>
public interface ILogRepository
{
    Task AddEmailLogAsync(EmailLog log);
}
