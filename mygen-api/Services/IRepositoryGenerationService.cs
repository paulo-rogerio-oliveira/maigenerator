using MyGenApi.Models;

namespace MyGenApi.Services;

public interface IRepositoryGenerationService
{
    Task<string> GenerateRepositoryAsync(string tableName);
} 