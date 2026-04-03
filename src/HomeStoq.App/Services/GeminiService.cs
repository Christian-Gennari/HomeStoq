using System.Text.Json;
using HomeStoq.App.Repositories;
using HomeStoq.Shared.DTOs;
using Microsoft.Extensions.AI;

namespace HomeStoq.App.Services;

public class GeminiService
{
    private readonly IChatClient _chatClient;
    private readonly string _language;
    private readonly ILogger<GeminiService> _logger;
    private readonly InventoryRepository _repository;
    private readonly PromptProvider _promptProvider;
    private readonly ChatOptions _chatOptions;

    public GeminiService(
        IChatClient chatClient,
        IConfiguration configuration,
        ILogger<GeminiService> logger,
        InventoryRepository repository,
        PromptProvider promptProvider
    )
    {
        _chatClient = chatClient;
        _logger = logger;
        _repository = repository;
        _promptProvider = promptProvider;
        _language = NormalizeLanguage(configuration["App:Language"]);

        // Define tools for the AI
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(_repository.GetStockLevel),
            AIFunctionFactory.Create(_repository.GetFullInventory),
            AIFunctionFactory.Create(_repository.GetConsumptionHistory),
        };

        _chatOptions = new ChatOptions { Tools = tools, ToolMode = ChatToolMode.Auto };
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "Swedish", StringComparison.OrdinalIgnoreCase))
            return "Swedish";
        return "English";
    }

    public async Task<List<ParsedVoiceActionDto>?> ParseVoiceCommandAsync(
        string text,
        IEnumerable<string>? existingItems = null
    )
    {
        var inventoryContext =
            existingItems != null && existingItems.Any()
                ? $"Current Inventory Items (PREFER THESE NAMES): {string.Join(", ", existingItems)}"
                : "";

        var systemPrompt = _promptProvider.GetParseVoiceCommandPrompt(_language, inventoryContext);

        var response = await _chatClient.GetResponseAsync(
            [new(ChatRole.System, systemPrompt), new(ChatRole.User, text)]
        );

        var cleaned = CleanJsonFromMarkdown(response.Text);
        if (string.IsNullOrEmpty(cleaned))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<ParsedVoiceActionDto>>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize voice command response: {Response}",
                cleaned
            );
            return null;
        }
    }

    public async Task<List<PantryItemDto>?> ProcessReceiptImageAsync(
        byte[] imageBytes,
        string mimeType = "image/jpeg",
        IEnumerable<string>? existingItems = null
    )
    {
        var inventoryContext =
            existingItems != null && existingItems.Any()
                ? $"Current Inventory Items (use these names to match): {string.Join(", ", existingItems)}"
                : "";

        var systemPrompt = _promptProvider.GetProcessReceiptImagePrompt(
            _language,
            inventoryContext
        );

        var response = await _chatClient.GetResponseAsync(
            [
                new(ChatRole.System, systemPrompt),
                new(
                    ChatRole.User,
                    [
                        new DataContent(imageBytes, mimeType),
                        new TextContent("Extract items from this receipt."),
                    ]
                ),
            ]
        );

        var cleaned = CleanJsonFromMarkdown(response.Text);
        if (string.IsNullOrEmpty(cleaned))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<PantryItemDto>>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize receipt scan response: {Response}",
                cleaned
            );
            return null;
        }
    }

    public async Task<ChatResponseDto> ChatAsync(
        string userMessage,
        List<ChatHistoryMessageDto>? history = null
    )
    {
        var messages = new List<ChatMessage>();
        var systemPrompt = _promptProvider.GetChatPrompt(_language);

        messages.Add(new ChatMessage(ChatRole.System, systemPrompt));

        if (history != null)
        {
            foreach (var msg in history)
            {
                // Skip initial assistant greeting - Gemini requires history to start with user message
                if (
                    messages.Count == 1
                    && msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                var role = msg.Role.ToLowerInvariant() switch
                {
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User,
                };
                messages.Add(new ChatMessage(role, msg.Text));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        var response = await _chatClient.GetResponseAsync(messages, _chatOptions);

        var clientHistory = history?.ToList() ?? new List<ChatHistoryMessageDto>();
        clientHistory.Add(new ChatHistoryMessageDto("user", userMessage));

        var replyText = response.Text ?? "";
        if (!string.IsNullOrEmpty(replyText))
        {
            clientHistory.Add(new ChatHistoryMessageDto("assistant", replyText));
        }

        return new ChatResponseDto { Reply = replyText, History = clientHistory };
    }

    public async Task<string?> GenerateShoppingListAsync(string historyJson, string inventoryJson)
    {
        var systemPrompt = _promptProvider.GetGenerateShoppingListPrompt(_language);

        var userMessage = $"History: {historyJson}\nInventory: {inventoryJson}";

        var response = await _chatClient.GetResponseAsync(
            [new(ChatRole.System, systemPrompt), new(ChatRole.User, userMessage)]
        );

        return CleanJsonFromMarkdown(response.Text);
    }

    private static string? CleanJsonFromMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned[7..];
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned[3..];
        cleaned = cleaned.Trim();
        if (cleaned.EndsWith("```"))
            cleaned = cleaned[..^3];
        return cleaned.Trim();
    }
}
