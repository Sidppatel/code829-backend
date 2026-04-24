using Db.Entities;

namespace Db.Repositories;

public class LogRepository(EventPlatformDbContext context) : ILogRepository
{
    public async Task AddEmailLogAsync(EmailLog log)
    {
        context.EmailLogs.Add(log);
        await context.SaveChangesAsync();
    }
}
