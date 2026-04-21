using Db.Entities;

namespace Db.Repositories;

public interface ILogRepository
{
    Task AddDeveloperLogAsync(DeveloperLog log);
    Task AddAdminLogAsync(BusinessLog log);
    Task AddSystemLogAsync(SystemLog log);
    Task AddEmailLogAsync(EmailLog log);
    Task<int> CleanupDeveloperLogsAsync(int retentionDays);
    Task<int> CleanupAdminLogsAsync(int retentionDays);
    Task<int> CleanupSystemLogsAsync(int retentionDays);
}
