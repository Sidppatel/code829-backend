using Db.Entities;

namespace Db.Repositories;

public interface IImageRepository
{
    Task<Image?> GetByIdAsync(Guid id);
    Task<List<Image>> GetByEntityAsync(string entityType, Guid entityId);
    Task<Image?> GetPrimaryAsync(string entityType, Guid entityId);
    Task AddAsync(Image image);
    Task DeleteAsync(Image image);
    Task SaveChangesAsync();
}
