using Db.Entities;

namespace Db.Repositories;

public interface ILogRepository
{
    Task AddEmailLogAsync(EmailLog log);
}
