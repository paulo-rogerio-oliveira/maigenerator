using Microsoft.AspNetCore.Http;

namespace MyGenApi.Services;

public class FileUploadService : IFileUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string type)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("No file was uploaded");
        }

        // Create directory if it doesn't exist
        var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadDir);

        // Use fixed filenames based on type
        var fileName = type == "model" ? "model.txt" : "repository.txt";
        var filePath = Path.Combine(uploadDir, fileName);

        try
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation($"File uploaded successfully: {filePath}");
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading file: {filePath}");
            throw;
        }
    }
} 