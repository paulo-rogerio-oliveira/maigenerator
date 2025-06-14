using System.Data;
using Microsoft.Data.SqlClient;
using MyGenApi.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
namespace MyGenApi.Services;

[McpServerToolType()]
public class ModelCreationService : IModelCreationService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ModelCreationService> _logger;
    
      public ModelCreationService()
    {

    }
   public ModelCreationService(IWebHostEnvironment environment, ILogger<ModelCreationService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

     [McpServerTool, Description("Create a model from a table")]
    public async Task<string> CreateModelToolAsync(string tableName)
    {
    
        try
        {
            // Try to read from body

           

            var configPath = Path.Combine(@"C:\projects\maigenerator\mygen-api", "App_Data", "config.json");
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
            if (string.IsNullOrEmpty(config?.connectionString))
            {
                return JsonSerializer.Serialize(new MCPResponse
                {
                    Success = false,
                    Error = "Connection string not found in configuration file"
                });
            }

      
            var modelContent = await CreateModelFromTableAsync(tableName, config.connectionString);
            
            return JsonSerializer.Serialize(new MCPResponse
            {
                Success = true,
                Data = new
                {
                    Content = modelContent,
                    TableName = tableName,
                    GeneratedAt = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new MCPResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
 
    public async Task<string> CreateModelFromTableAsync(string tableName, string connectionString)
    {
        try
        {
            // Read the model template file
            var modelTemplatePath = Path.Combine(_environment.ContentRootPath, "uploads", "model.txt");
            if (!File.Exists(modelTemplatePath))
            {
                throw new FileNotFoundException("Model template file not found");
            }
            var modelTemplate = await File.ReadAllTextAsync(modelTemplatePath);

            // Get table metadata
            var columns = await GetTableColumnsAsync(tableName, connectionString);

            // Create model content
            var modelContent = new System.Text.StringBuilder();
            modelContent.AppendLine($"// Table: {tableName}");
            modelContent.AppendLine("// Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            modelContent.AppendLine();
            modelContent.AppendLine("// Example from model.txt:");
            modelContent.AppendLine(modelTemplate);
            modelContent.AppendLine();
            modelContent.AppendLine("// Table Metadata:");
            modelContent.AppendLine("// Columns:");
            foreach (var column in columns)
            {
   
                modelContent.AppendLine($"// - {column.ColumnName} ({column.DataType})");
            }

            return modelContent.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating model for table {tableName}");
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

    private async Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync(string tableName, string connectionString)
    {
        const string sql = @"
            SELECT 
                COLUMN_NAME,
                REFERENCED_TABLE_NAME,
                REFERENCED_COLUMN_NAME
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            WHERE TABLE_NAME = @TableName
            AND REFERENCED_TABLE_NAME IS NOT NULL;";

        var result = new List<ForeignKeyInfo>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TableName", tableName);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ForeignKeyInfo
            {
                ColumnName = reader.GetString(0),
                ReferencedTable = reader.GetString(1),
                ReferencedColumn = reader.GetString(2)
            });
        }

        return result;
    }
} 