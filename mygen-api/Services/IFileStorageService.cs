namespace MyGenApi.Services;

public interface IFileStorageService
{
    Task SaveAsync(string fileName, string content);
    Task<string?> LoadAsync(string fileName);
} 