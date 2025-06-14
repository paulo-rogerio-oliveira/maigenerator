using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using MyGenApi.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using System.ComponentModel;
using MyGenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});




builder.Services.AddScoped<ITableMetadataService, TableMetadataService>();
builder.Services.AddHttpClient<ICodeGenService, CodeGenService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IModelCreationService, ModelCreationService>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();

// MCP Tool endpoints
app.MapGet("/mcp/tools", () =>
{
    return Results.Ok(new
    {
        tools = new[]
        {
            new
            {
                name = "model-creator",
                description = "Creates a model from a database table",
                version = "1.0.0",
                endpoint = "/mcp/model/create"
            }
        }
    });
})
.WithName("MCPTools")
.WithOpenApi();

Func<string> getFolder = () => Path.Combine(app.Environment.ContentRootPath, "App_Data");

app.MapPost("/mcp/model/create", async (MCPModelRequest request, HttpContext context) =>
{
    string? tableName = null;
    try
    {
        // Try to read from body

        if (request != null && !string.IsNullOrWhiteSpace(request.TableName))
        {
            tableName = request.TableName;
        }
        else
        {
            // Fallback to query string
            tableName = context.Request.Query["tableName"].ToString();
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return Results.BadRequest(new MCPResponse
            {
                Success = false,
                Error = "Table name must be provided in the body or as a query parameter."
            });
        }

        var configPath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "config.json");
        if (!File.Exists(configPath))
        {
            Console.WriteLine(configPath);
            return Results.BadRequest(new MCPResponse
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
            return Results.BadRequest(new MCPResponse
            {
                Success = false,
                Error = "Connection string not found in configuration file"
            });
        }

        var modelService = context.RequestServices.GetRequiredService<IModelCreationService>();
        var modelContent = await modelService.CreateModelFromTableAsync(tableName, config.connectionString);
        
        return Results.Ok(new MCPResponse
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
        return Results.BadRequest(new MCPResponse
        {
            Success = false,
            Error = ex.Message
        });
    }
})
.WithName("MCPModelCreate")
.WithOpenApi();

app.MapPost("/metadata/{tableName}", async (string tableName, TableListRequest req, ITableMetadataService service) =>
{
    var columns = await service.GetColumnsAsync(tableName, req.ConnectionString);
    return Results.Ok(columns);
})
.WithName("GetTableMetadata")
.WithOpenApi();

app.MapPost("/codegen", async (CodeGenRequest req, ICodeGenService codeGenService) =>
{
    Console.WriteLine($"Prompt: {req.Prompt}");
    Console.WriteLine($"OpenAiKey: {req.OpenAiKey}");
    var result = await codeGenService.GenerateCodeAsync(req.Prompt, req.OpenAiKey);
    return Results.Ok(result);
})
.WithName("CodeGen");

app.MapPost("/file/save", async (FileSaveRequest req, IFileStorageService fileService) =>
{
    await fileService.SaveAsync(req.FileName, req.Content);
    return Results.Ok();
})
.WithName("SaveFile");

app.MapGet("/file/load/{fileName}", async (string fileName, IFileStorageService fileService) =>
{
    var content = await fileService.LoadAsync(fileName);
    return content is not null ? Results.Ok(content) : Results.NotFound();
})
.WithName("LoadFile");

app.MapGet("/file/exists/{type}", async (string type, IWebHostEnvironment env) =>
{
    var fileName = type == "model" ? "model.txt" : "repository.txt";
    var filePath = Path.Combine(env.ContentRootPath, "uploads", fileName);
    return Results.Ok(new { exists = File.Exists(filePath), path = filePath });
})
.WithName("CheckFileExists")
.WithOpenApi();

app.MapPost("/test-connection", async (TestConnectionRequest req, ITableMetadataService service) =>
{
    try
    {
        var ok = await service.TestConnectionAsync(req.ConnectionString);
        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, error = ex.Message });
    }
});

app.MapPost("/tables", async (TableListRequest req, ITableMetadataService service) =>
{
    try
    {
        Console.WriteLine($"ConnectionString from tables: {req.ConnectionString}");
        var tables = await service.GetAllTableNamesAsync(req.ConnectionString);
        Console.WriteLine("finished tables");
        return Results.Ok(tables);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        return Results.Ok(new { success = false, error = ex.Message });
    }
});

app.MapGet("/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { token = tokens.RequestToken });
})
.WithName("GetAntiforgeryToken")
.WithOpenApi();

app.MapPost("/file/upload/{type}", async (string type, IFormFile file, IFileUploadService uploadService) =>
{
    try
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest("No file was uploaded");
        }

        if (type != "model" && type != "repository")
        {
            return Results.BadRequest("Invalid file type. Must be 'model' or 'repository'");
        }

        var fileName = await uploadService.UploadFileAsync(file, type);
        return Results.Ok(new { fileName });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UploadFile")
.WithOpenApi();
var modelCreationService = new ModelCreationService();


app.Run();

record FileSaveRequest(string FileName, string Content);
record TestConnectionRequest(string ConnectionString);
record TableListRequest(string ConnectionString);
record CodeGenRequest(string Prompt, string? OpenAiKey);
record MCPModelRequest(string TableName);

record ConfigFile(string connectionString, string openAiKey);


