using Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace Db.Repositories;

public class ImageRepository(EventPlatformDbContext context) : IImageRepository
{
    public async Task<Image?> GetByIdAsync(Guid id)
        => await context.Images.FirstOrDefaultAsync(i => i.Id == id);

    public async Task<List<Image>> GetByEntityAsync(string entityType, Guid entityId)
        => await context.Images
            .Where(i => i.EntityType == entityType && i.EntityId == entityId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

    public async Task<Image?> GetPrimaryAsync(string entityType, Guid entityId)
        => await context.Images
            .Where(i => i.EntityType == entityType && i.EntityId == entityId)
            .OrderBy(i => i.SortOrder)
            .FirstOrDefaultAsync();

    public async Task AddAsync(Image image)
        => await context.Images.AddAsync(image);

    public async Task DeleteAsync(Image image)
    {
        context.Images.Remove(image);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
        => await context.SaveChangesAsync();
}
