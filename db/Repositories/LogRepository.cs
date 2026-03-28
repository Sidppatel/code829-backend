using Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace Db.Repositories;

public class LogRepository(EventPlatformDbContext context) : ILogRepository
{
    public async Task AddDeveloperLogAsync(DeveloperLog log)
    {
        context.DeveloperLogs.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task AddAdminLogAsync(AdminLog log)
    {
        context.AdminLogs.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task AddSystemLogAsync(SystemLog log)
    {
        context.SystemLogs.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task AddEmailLogAsync(EmailLog log)
    {
        context.EmailLogs.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task<int> CleanupDeveloperLogsAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        return await context.DeveloperLogs
            .Where(l => l.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }

    public async Task<int> CleanupAdminLogsAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        return await context.AdminLogs
            .Where(l => l.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }

    public async Task<int> CleanupSystemLogsAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        return await context.SystemLogs
            .Where(l => l.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }
}
