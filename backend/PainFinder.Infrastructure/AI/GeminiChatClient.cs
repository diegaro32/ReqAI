using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace PainFinder.Infrastructure.AI;

/// <summary>
/// IChatClient implementation for Google Gemini REST API.
/// Uses Microsoft.Extensions.AI abstractions.
/// </summary>
public sealed class GeminiChatClient(
    HttpClient httpClient,
    string apiKey,
    string model = "gemini-2.5-flash-lite",
    int thinkingBudget = 0) : IChatClient
{
    public ChatClientMetadata Metadata => new("Gemini", null, model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var contents = chatMessages.Select(m => new
        {
            role = m.Role == ChatRole.User ? "user" : "model",
            parts = new[] { new { text = m.Text ?? string.Empty } }
        }).ToArray();

        var requestBody = new
        {
            contents,
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.3,
                maxOutputTokens = 32768,
                thinkingConfig = new
                {
                    thinkingBudget
                }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var responseMessage = new ChatMessage(ChatRole.Assistant, text);
        return new ChatResponse([responseMessage]);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(chatMessages, options, cancellationToken);
        var text = response.Messages[^1].Text ?? string.Empty;
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(GeminiChatClient)) return this;
        return null;
    }

    public void Dispose() { }
}
