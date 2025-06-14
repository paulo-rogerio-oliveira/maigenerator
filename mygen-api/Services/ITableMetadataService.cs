namespace MyGenApi.Services;

using MyGenApi.Models;

public interface ITableMetadataService
{
    Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableName);
    Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableName, string connectionString);
    Task<bool> TestConnectionAsync(string connectionString);
    Task<IEnumerable<string>> GetAllTableNamesAsync();
    Task<IEnumerable<string>> GetAllTableNamesAsync(string connectionString);
} 