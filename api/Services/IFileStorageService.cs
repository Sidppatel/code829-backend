namespace Api.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream fileStream, string entityType, string fileName);
    Task SaveWithKeyAsync(Stream fileStream, string key, string contentType);
    Task<bool> DeleteAsync(string path);
    string GetPublicUrl(string path);
}
