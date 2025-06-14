using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyGenApi.Services;

public class CodeGenService : ICodeGenService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;

    public CodeGenService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _endpoint = configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
    }

    public async Task<string> GenerateCodeAsync(string prompt, string? openAiKey = null)
    {
        var apiKeyToUse = string.IsNullOrWhiteSpace(openAiKey) ? _apiKey : openAiKey;
        Console.WriteLine($"apiKeyToUse: {apiKeyToUse}");
        var requestBody = new
        {
            model = "gpt-4.1",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 2048,
            stream = true
        };

        var requestJson = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKeyToUse);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var sb = new StringBuilder();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith("data: "))
            {
                var json = line.Substring(6).Trim();
                if (json == "[DONE]")
                    break;
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var content = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta")
                        .GetProperty("content").GetString();
                    if (!string.IsNullOrEmpty(content))
                        sb.Append(content);
                }
                catch { /* ignore malformed lines */ }
            }
        }
        Console.WriteLine($"sb: {sb}");
        return sb.ToString();
    }
} 