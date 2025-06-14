using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Text;
using MyGenApi.Models;

namespace MyGenApi.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _basePath;
    private readonly string _uploadsPath;

    public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger, IConfiguration configuration)
    {
        _environment = environment;
        _logger = logger;

        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>();
        var baseAppPath = appSettings?.BaseAppPath;

        if (string.IsNullOrEmpty(baseAppPath))
        {
            baseAppPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            _logger.LogInformation("Using default executable path as base path: {BasePath}", baseAppPath);
        }
        else
        {
            _logger.LogInformation("Using configured base path: {BasePath}", baseAppPath);
        }

        _basePath = Path.Combine(baseAppPath, "App_Data");
        _uploadsPath = Path.Combine(baseAppPath, "uploads");

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            _logger.LogInformation("Created App_Data directory at: {Path}", _basePath);
        }

        if (!Directory.Exists(_uploadsPath))
        {
            Directory.CreateDirectory(_uploadsPath);
            _logger.LogInformation("Created uploads directory at: {Path}", _uploadsPath);
        }
    }

    public async Task SaveAsync(string fileName, string content)
    {
        try
        {
            var filePath = Path.Combine(_basePath, fileName);
            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            _logger.LogInformation($"File saved successfully: {fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error saving file: {fileName}");
            throw;
        }
    }

    public async Task<string?> LoadAsync(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_basePath, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning($"File not found: {fileName}");
                return null;
            }
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            _logger.LogInformation($"File loaded successfully: {fileName}");
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading file: {fileName}");
            throw;
        }
    }

    public async Task<string> SaveFileAsync(IFormFile file)
    {
        try
        {
            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(_uploadsPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation($"File saved successfully: {fileName}");
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file");
            throw;
        }
    }

    public void DeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation($"File deleted successfully: {filePath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting file: {filePath}");
            throw;
        }
    }
} 