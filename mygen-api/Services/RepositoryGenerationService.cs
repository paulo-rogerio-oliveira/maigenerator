using System.Data;
using Microsoft.Data.SqlClient;
using MyGenApi.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Reflection;

namespace MyGenApi.Services;

[McpServerToolType()]
public class RepositoryGenerationService : IRepositoryGenerationService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RepositoryGenerationService> _logger;
    private readonly string _basePath;

    public RepositoryGenerationService(IWebHostEnvironment environment, ILogger<RepositoryGenerationService> logger, IConfiguration configuration)
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

        _basePath = baseAppPath;
    }

    [McpServerTool, Description("Generate a repository for a table")]
    public async Task<string> GenerateRepositoryAsync(string tableName)
    {
        try
        {
            // Read the repository template file
            var templatePath = Path.Combine(_basePath, "uploads", "repository.txt");
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("Repository template file not found");
            }
            
            var configPath = Path.Combine(_basePath, "App_Data", "config.json");
            if (!File.Exists(configPath))
            {
                Console.WriteLine(configPath);
                return JsonSerializer.Serialize(new MCPResponse
                {
                    Success = false,
                    Error = "Configuration file not found in App_Data directory"
                });
            }

            var configJson = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<ConfigFile>(configJson);
            Console.WriteLine(configJson);
            
            var template = await File.ReadAllTextAsync(templatePath);

            // Get table metadata
            var columns = await GetTableColumnsAsync(tableName, config.connectionString);
  

            // Create repository content
            var repositoryContent = new System.Text.StringBuilder();
            repositoryContent.AppendLine($"// Repository for table: {tableName}");
            repositoryContent.AppendLine("// Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            repositoryContent.AppendLine();
            repositoryContent.AppendLine("// Example from repository.txt:");
            repositoryContent.AppendLine(template);
            repositoryContent.AppendLine();
            repositoryContent.AppendLine("// Table Metadata:");
                       
            repositoryContent.AppendLine("// Columns:");
            foreach (var column in columns)
            {
                repositoryContent.AppendLine($"// - {column.ColumnName} ({column.DataType})");
            }

            return repositoryContent.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating repository for table {tableName}");
            throw;
        }
    }

    private async Task<IEnumerable<ColumnInfo>> GetTableColumnsAsync(string tableName, string connectionString)
    {
        const string sql = @"
            SELECT 
                COLUMN_NAME,
                DATA_TYPE,
                CHARACTER_MAXIMUM_LENGTH,
                IS_NULLABLE,
                COLUMN_DEFAULT
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName
            ORDER BY ORDINAL_POSITION;";

        var result = new List<ColumnInfo>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", tableName);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1)
            });
        }

        return result;
    }

    private async Task<IEnumerable<string>> GetPrimaryKeysAsync(string tableName, string connectionString)
    {
        const string sql = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
            AND TABLE_NAME = @TableName;";

        var result = new List<string>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", tableName);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
} 