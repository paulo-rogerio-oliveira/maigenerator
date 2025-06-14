namespace MyGenApi.Services;

public interface IFileUploadService
{
    Task<string> UploadFileAsync(IFormFile file, string type);
} 