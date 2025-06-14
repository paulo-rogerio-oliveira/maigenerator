using System.Text;

namespace MyGenApi.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public FileStorageService(IWebHostEnvironment env)
    {
        _basePath = Path.Combine(env.ContentRootPath, "App_Data");
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    public async Task SaveAsync(string fileName, string content)
    {
        var filePath = Path.Combine(_basePath, fileName);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
    }

    public async Task<string?> LoadAsync(string fileName)
    {
        var filePath = Path.Combine(_basePath, fileName);
        if (!File.Exists(filePath))
            return null;
        return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
    }
} 