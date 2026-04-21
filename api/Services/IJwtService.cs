using Db.Entities;

namespace Api.Services;

public interface IJwtService
{
    Task<string> GenerateUserJwtAsync(User user);
    Task<string> GenerateAdminJwtAsync(BusinessUser admin);
}
