using System.ComponentModel;
using System.Text.Json;
using HomeStoq.App.Endpoints;
using HomeStoq.App.Models;
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
            AIFunctionFactory.Create(GetSavedShoppingList),
        };

        _chatOptions = new ChatOptions { Tools = tools, ToolMode = ChatToolMode.Auto };
    }

    [Description("Gets the user's current saved shopping list if one exists. Returns the list name and all items.")]
    public async Task<string?> GetSavedShoppingList()
    {
        var savedList = await _repository.GetSavedBuyListAsync();
        if (savedList == null)
        {
            return _language == "Swedish" ? "Ingen sparad inköpslista finns just nu." : "No saved shopping list exists right now.";
        }

        var items = savedList.Items
            .Where(i => !i.IsDismissed)
            .Select(i => $"- {i.ItemName} x{i.Quantity}")
            .ToList();

        var result = _language == "Swedish" 
            ? $"Sparad lista: \"{savedList.SavedName}\" med {items.Count} varor:\n{string.Join("\n", items)}"
            : $"Saved list: \"{savedList.SavedName}\" with {items.Count} items:\n{string.Join("\n", items)}";

        return result;
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

    public async Task<ShoppingBuddyResponse?> GenerateShoppingBuddyListAsync(string historyJson, string inventoryJson)
    {
        var systemPrompt = _promptProvider.GetShoppingBuddyPrompt(_language);

        var userMessage = $@"Here is my pantry data:

CURRENT INVENTORY:
{inventoryJson}

PURCHASE HISTORY (last 30 days):
{historyJson}

Generate a helpful shopping list with explanations.";

        var response = await _chatClient.GetResponseAsync(
            [new(ChatRole.System, systemPrompt), new(ChatRole.User, userMessage)]
        );

        var cleaned = CleanJsonFromMarkdown(response.Text);
        if (string.IsNullOrEmpty(cleaned))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ShoppingBuddyResponse>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize shopping buddy response: {Response}", cleaned);
            return null;
        }
    }

    public async Task<ShoppingBuddyResponse?> GenerateShoppingBuddyListWithContextAsync(string historyJson, string inventoryJson, string userContext, string? previousSuggestionsJson = null)
    {
        var systemPrompt = _promptProvider.GetShoppingBuddyPrompt(_language);
        var followUpPrompt = _promptProvider.GetShoppingBuddyFollowUpPrompt(_language, userContext);

        var userMessage = $@"Here is my pantry data:

CURRENT INVENTORY:
{inventoryJson}

PURCHASE HISTORY (last 30 days):
{historyJson}

{(previousSuggestionsJson != null ? $"PREVIOUS SUGGESTIONS:\n{previousSuggestionsJson}\n\n" : "")}

{followUpPrompt}";

        var response = await _chatClient.GetResponseAsync(
            [new(ChatRole.System, systemPrompt), new(ChatRole.User, userMessage)]
        );

        var cleaned = CleanJsonFromMarkdown(response.Text);
        if (string.IsNullOrEmpty(cleaned))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ShoppingBuddyResponse>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize shopping buddy follow-up response: {Response}", cleaned);
            return null;
        }
    }

    public async Task<ShoppingListChatResponse?> ChatWithShoppingListAsync(
        string userMessage,
        List<ChatMessageDto> conversationHistory,
        List<BuyListItemDto> currentItems,
        string inventoryJson,
        string language)
    {
        var systemPrompt = _promptProvider.GetShoppingListChatPrompt(language);
        
        // Build conversation context with action tracking
        var conversationContext = string.Join("\n", conversationHistory.Select(m =>
        {
            var line = $"{m.Role}: {m.Content}";
            if (m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(m.ActionsJson))
            {
                try
                {
                    var actions = JsonSerializer.Deserialize<List<ChatActionItem>>(m.ActionsJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (actions != null && actions.Count > 0)
                    {
                        var actionSummary = string.Join(", ", actions.Select(a =>
                            $"{a.Type}: {a.ItemName} x{a.Quantity}"));
                        line += $"\n[ACTIONS TAKEN: {actionSummary}]";
                    }
                }
                catch { }
            }
            return line;
        }));
        var currentItemsJson = JsonSerializer.Serialize(currentItems);
        
        var fullPrompt = $"CONVERSATION HISTORY:\n{conversationContext}\n\nCURRENT SHOPPING LIST:\n{currentItemsJson}\n\nPANTRY INVENTORY:\n{inventoryJson}\n\nUSER MESSAGE: {userMessage}\n\nRespond with the JSON format specified in your instructions.";;

        var response = await _chatClient.GetResponseAsync(
            [new(ChatRole.System, systemPrompt), new(ChatRole.User, fullPrompt)]
        );

        var cleaned = CleanJsonFromMarkdown(response.Text);
        if (string.IsNullOrEmpty(cleaned))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ShoppingListChatResponse>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize shopping list chat response: {Response}", cleaned);
            return null;
        }
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

// DTO for shopping buddy response
public class ShoppingBuddyResponse
{
    public string Greeting { get; set; } = string.Empty;
    public List<ShoppingBuddySuggestion> Suggestions { get; set; } = new();
    public string FollowUpQuestion { get; set; } = string.Empty;
}

public class ShoppingBuddySuggestion
{
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string Confidence { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}

// NEW: Chat response for conversational shopping list
public class ShoppingListChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<ChatActionItem> Actions { get; set; } = new();
    public List<string> SuggestedReplies { get; set; } = new();
    public bool RequiresConfirmation { get; set; }
}

public class ChatActionItem
{
    public string Type { get; set; } = string.Empty; // "add", "remove", "modify", "info"
    public string? ItemName { get; set; }
    public double Quantity { get; set; }
    public string? Category { get; set; }
    public string? Reasoning { get; set; }
}

// DTOs defined in ShoppingListEndpoints.cs to avoid circular references
