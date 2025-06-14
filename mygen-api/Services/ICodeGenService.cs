namespace MyGenApi.Services;

public interface ICodeGenService
{
    Task<string> GenerateCodeAsync(string prompt, string? openAiKey = null);
} 