namespace Db.Repositories.StoredProcedures;

public interface ILogProcedures
{
    Task<Guid> CreateEmailLogAsync(string recipient, string subject, string? body, string status, CancellationToken ct = default);
    Task<int> CleanupOldLogsAsync(int devDays, int adminDays, int systemDays, CancellationToken ct = default);
}
