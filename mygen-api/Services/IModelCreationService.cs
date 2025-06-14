using MyGenApi.Models;

namespace MyGenApi.Services;

public interface IModelCreationService
{
    Task<string> CreateModelFromTableAsync(string tableName, string connectionString);
} 