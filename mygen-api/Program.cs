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
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Get base path from command line arguments
var basePathArg = args.FirstOrDefault(arg => arg.StartsWith("--base-path="));
if (!string.IsNullOrEmpty(basePathArg))
{
    var basePath = basePathArg.Split('=')[1];
    builder.Configuration["AppSettings:BaseAppPath"] = basePath;
    Console.WriteLine($"Using base path from command line: {basePath}");
}

var modelCreationService = new ModelCreationService(@"C:\projects\maigenerator\mygen-api\bin\release\net8.0\win-x64\publish");
var modelContent = await modelCreationService.CreateModelToolAsync("unidade");
Console.WriteLine(modelContent);    

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
});

builder.WebHost.UseUrls($"http://*:5186");

builder.Services.AddScoped<ITableMetadataService, TableMetadataService>();
builder.Services.AddHttpClient<ICodeGenService, CodeGenService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IModelCreationService, ModelCreationService>();
builder.Services.AddScoped<IRepositoryGenerationService, RepositoryGenerationService>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure CORS with credentials
app.UseCors(x => x
    .SetIsOriginAllowed(origin => true)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

app.UseAntiforgery();

// Add antiforgery token endpoint
app.MapGet("/antiforgery/token", (IAntiforgery antiforgery, HttpContext context) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Json(new { token = tokens.RequestToken });
});

// Update file upload endpoint to handle antiforgery token
app.MapPost("/file/upload/{type}", async (string type, IFormFile file, IFileUploadService uploadService, HttpContext context, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);

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
            },
            new
            {
                name = "repository-generator",
                description = "Generates a repository for a database table",
                version = "1.0.0",
                endpoint = "/mcp/repository/generate"
            }
        }
    });
})
.WithName("MCPTools")
.WithOpenApi();

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

app.MapPost("/mcp/repository/generate", async (MCPModelRequest request, HttpContext context) =>
{
    string? tableName = null;
    try
    {
        if (request != null && !string.IsNullOrWhiteSpace(request.TableName))
        {
            tableName = request.TableName;
        }
        else
        {
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

        var repositoryService = context.RequestServices.GetRequiredService<IRepositoryGenerationService>();
        var repositoryContent = await repositoryService.GenerateRepositoryAsync(tableName);
        
        return Results.Ok(new MCPResponse
        {
            Success = true,
            Data = new
            {
                Content = repositoryContent,
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
.WithName("MCPRepositoryGenerate")
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

app.MapControllers();

app.Run();

record FileSaveRequest(string FileName, string Content);
record TestConnectionRequest(string ConnectionString);
record TableListRequest(string ConnectionString);
record CodeGenRequest(string Prompt, string? OpenAiKey);
record MCPModelRequest(string TableName);

record ConfigFile(string connectionString, string openAiKey);


