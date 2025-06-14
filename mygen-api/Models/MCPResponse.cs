namespace MyGenApi.Models;

public record MCPResponse
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string? Error { get; init; }
} 