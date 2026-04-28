namespace Db.Repositories.StoredProcedures;

/// <summary>
/// Email-log + retention cleanup via stored procedures. Admin/developer/system log
/// writes go through IAuditLogService → audit_logs; legacy Create*LogAsync methods
/// were removed in the DropLegacyLogTables migration. CleanupOldLogsAsync still
/// takes three retention knobs (dev/admin/system) — they now map to ActorType
/// filters against audit_logs inside sp_cleanup_old_logs.
/// </summary>
public interface ILogProcedures
{
    Task<Guid> CreateEmailLogAsync(string recipient, string subject, string? body, string status, CancellationToken ct = default);
    Task<int> CleanupOldLogsAsync(int devDays, int adminDays, int systemDays, CancellationToken ct = default);
}
