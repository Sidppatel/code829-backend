namespace Api.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream fileStream, string entityType, string fileName);
    Task<bool> DeleteAsync(string path);
    string GetPublicUrl(string path);
}
