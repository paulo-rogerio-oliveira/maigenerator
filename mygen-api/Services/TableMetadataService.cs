using System.Data;
using Microsoft.Data.SqlClient;
using MyGenApi.Models;

namespace MyGenApi.Services;

public class TableMetadataService : ITableMetadataService
{
    private readonly string _connectionString;

    public TableMetadataService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableName)
    {
        const string sql = @"SELECT COLUMN_NAME, DATA_TYPE
                                FROM INFORMATION_SCHEMA.COLUMNS
                                WHERE TABLE_NAME = @TableName
                                ORDER BY ORDINAL_POSITION;";

        var result = new List<ColumnInfo>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text
        };
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

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            const string sql = @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS";
                        
            await using var connection = new SqlConnection(connectionString);
            connection.Open();
            await using var command = new SqlCommand(sql, connection)
            {
                CommandType = CommandType.Text
            };
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAllTableNamesAsync()
    {
        const string sql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;";
        var result = new List<string>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        Console.WriteLine("connection opened");
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Console.WriteLine("reader opened");
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public async Task<IEnumerable<string>> GetAllTableNamesAsync(string connectionString)
    {
        const string sql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;";
        var result = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public async Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableName, string connectionString)
    {
        const string sql = @"SELECT COLUMN_NAME, DATA_TYPE
                                FROM INFORMATION_SCHEMA.COLUMNS
                                WHERE TABLE_NAME = @TableName
                                ORDER BY ORDINAL_POSITION;";

        var result = new List<ColumnInfo>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text
        };
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
} 