using System.Text.Json;
using HomeStoq.App.Repositories;
using HomeStoq.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using HomeStoq.App.Configuration;

namespace HomeStoq.App.Services;

public class GeminiService
{
    private readonly IChatClient _chatClient;
    private readonly string _language;
    private readonly ILogger<GeminiService> _logger;
    private readonly InventoryRepository _repository;
    private readonly ChatOptions _chatOptions;

    public GeminiService(
        IChatClient chatClient,
        IOptions<AppOptions> appOptions,
        ILogger<GeminiService> logger,
        InventoryRepository repository
    )
    {
        _chatClient = chatClient;
        _logger = logger;
        _repository = repository;
        _language = NormalizeLanguage(appOptions.Value.Language);

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

    public async Task<List<ParsedVoiceAction>?> ParseVoiceCommandAsync(
        string text,
        IEnumerable<string>? existingItems = null
    )
    {
        var inventoryContext =
            existingItems != null && existingItems.Any()
                ? $"Current Inventory Items (PREFER THESE NAMES): {string.Join(", ", existingItems)}"
                : "";

        var systemPrompt =
            _language == "Swedish"
                ? $@"You are a food inventory assistant. ALL communication, reasoning, and output MUST be in Swedish. Interpret the intent.
{inventoryContext}
Guidelines:
1. Return the ItemName in Swedish.
2. Normalize ItemNames. Prefer existing simplified names.
3. Identify ItemName, Action (Add or Remove), and Quantity.
4. Categories: [Mejeri, Frukt/Grönt, Skafferi, Kött/Fisk, Bageri, Frysvaror, Hushåll, Övrigt].
5. Default quantity to 1. For ""all""/""everything"" use 9999.
6. Respond ONLY with a JSON array of objects.
Format: [ {{ ""ItemName"": ""Mjölk"", ""Action"": ""Remove"", ""Quantity"": 1, ""Category"": ""Mejeri"" }} ]"
                : $@"You are a food inventory assistant. Interpret intent.
{inventoryContext}
Guidelines:
1. Normalize ItemNames. Prefer existing names.
2. Identify ItemName, Action, Quantity.
3. Categories: [Dairy, Produce, Pantry, Meat/Fish, Bakery, Frozen, Household, Other].
4. Default quantity 1. ""All""/""everything"" = 9999.
5. Respond ONLY with a JSON array.
Format: [ {{ ""ItemName"": ""Milk"", ""Action"": ""Remove"", ""Quantity"": 1, ""Category"": ""Dairy"" }} ]";

        var response = await _chatClient.GetResponseAsync(
            [new(ChatRole.System, systemPrompt), new(ChatRole.User, text)]
        );

        var cleaned = CleanJsonFromMarkdown(response.Text);
        if (string.IsNullOrEmpty(cleaned))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<ParsedVoiceAction>>(
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

    public async Task<List<PantryItem>?> ProcessReceiptImageAsync(
        byte[] imageBytes,
        string mimeType = "image/jpeg",
        IEnumerable<string>? existingItems = null
    )
    {
        var inventoryContext =
            existingItems != null && existingItems.Any()
                ? $"Current Inventory Items (use these names to match): {string.Join(", ", existingItems)}"
                : "";

        var systemPrompt =
            _language == "Swedish"
                ? $@"You are a system that reads Swedish grocery receipts. ALL communication, reasoning, and output MUST be in Swedish.
{inventoryContext}
RULES:
1. Return ALL item names in SWEDISH.
2. Step 1: Extract the EXACT text from the receipt (`ReceiptText`).
3. Step 2: Decipher truncated/abbreviated text into a readable full product name (`ExpandedName`). Capitalize properly.
4. Step 3: Set `ItemName` to the EXACT value of `ExpandedName`. Do NOT change, shorten, or normalize it. Keep every word.
5. Categories: [Mejeri, Frukt/Grönt, Skafferi, Kött/Fisk, Bageri, Frysvaror, Hushåll, Övrigt].
6. Use the inventory list to MATCH EXISTING `ItemName`s only. If no existing item matches, use the ExpandedName as-is.
7. NEVER use a category word (e.g. ""Vegetariskt"", ""Mellanmål"", ""Dryck"", ""Ekologiskt"") as an ItemName. If the extracted text is a category label, use the full product name instead.
8. Ignore: deposits, bags, discounts, totals, loyalty points, packaging fees, receipt footer text.
9. Extract the EXACT unit price if visible.
10. Respond ONLY with a JSON array.
EXAMPLES:
- Receipt text ""Gammaldags idealma"" → ExpandedName: ""Gammaldags Idealmakaroner"", ItemName: ""Gammaldags Idealmakaroner""
- Receipt text ""Pasta w/arrabiata"" → ExpandedName: ""Pastasås Arrabiata"", ItemName: ""Pastasås Arrabiata""
- Receipt text ""Ekologisk mjölk 1l"" → ExpandedName: ""Ekologisk Mjölk 1l"", ItemName: ""Ekologisk Mjölk 1l""
- Receipt text ""Vegetariskt"" → ExpandedName: ""Vegetariskt"", ItemName: ""Vegetariskt"" ← WRONG, use actual product name from receipt
Format: [ {{ ""ReceiptText"": ""Gammaldags idealma"", ""ExpandedName"": ""Gammaldags Idealmakaroner"", ""ItemName"": ""Gammaldags Idealmakaroner"", ""Quantity"": 1, ""Price"": 34.90, ""Category"": ""Skafferi"" }} ]"
                : $@"You are a system that reads grocery receipts.
{inventoryContext}
RULES:
1. Extract the EXACT text from the receipt (`ReceiptText`).
2. Decipher truncated text into a readable full product name (`ExpandedName`). Capitalize properly.
3. Set `ItemName` to the EXACT value of `ExpandedName`. Do NOT change, shorten, or normalize it. Keep every word.
4. Categories: [Dairy, Produce, Pantry, Meat/Fish, Bakery, Frozen, Household, Other].
5. Use the inventory list to MATCH EXISTING `ItemName`s only. If no existing item matches, use the ExpandedName as-is.
6. NEVER use a category word (e.g. ""Vegetarian"", ""Snack"", ""Beverage"") as an ItemName. If the extracted text is a category label, use the full product name instead.
7. Ignore: deposits, bags, discounts, totals, loyalty points, packaging fees, receipt footer text.
8. Extract the EXACT unit price if visible.
9. Respond ONLY with a JSON array.
EXAMPLES:
- Receipt text ""Gammaldags idealma"" → ExpandedName: ""Gammaldags Idealmakaroner"", ItemName: ""Gammaldags Idealmakaroner""
- Receipt text ""Skim Milk Org 1l"" → ExpandedName: ""Skimmed Milk Organic 1l"", ItemName: ""Skimmed Milk Organic 1l""
Format: [ {{ ""ReceiptText"": ""Gammaldags idealma"", ""ExpandedName"": ""Gammaldags Idealmakaroner"", ""ItemName"": ""Gammaldags Idealmakaroner"", ""Quantity"": 1, ""Price"": 34.90, ""Category"": ""Skafferi"" }} ]";

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
            return JsonSerializer.Deserialize<List<PantryItem>>(
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

    public async Task<Contracts.ChatResponse> ChatAsync(
        string userMessage,
        List<ChatHistoryMessage>? history = null
    )
    {
        var messages = new List<ChatMessage>();
        var systemPrompt =
            _language == "Swedish"
                ? @"Du är en hjälpsam assistent för HomeStoq-skafferiet. Du kan svara på frågor, föra konversationer och komma ihåg tidigare meddelanden i denna chatt. För frågor om lagersaldo och konsumtionshistorik, använd de tillgängliga verktygen. Svara alltid på svenska.

SVARFORMAT:
- För listor och historik, använd ALLTID ett streck-prefix per rad, aldrig en lång sammanhängande text.
- Forma varje rad som: ""- [Datum]: [Produkt] x[Antal] ([Pris] kr)"" eller ""- [Produkt] x[Antal]"".
- Sortera chronologiskt med det nyaste först.
- Om du visar lager: ""- [Produkt]: [Antal] st""
- Håll svar korta och läsbara. Max 10 rader per kategori.
- För övriga frågor, svara kort och direkt."
                : @"You are a helpful assistant for the HomeStoq pantry. You can answer questions, have conversations, and remember previous messages in this chat. For questions about stock levels and consumption history, use the available tools.

RESPONSE FORMAT:
- For list and history, ALWAYS use dash-prefixed lines, NEVER a single long paragraph.
- Format each line as: ""- [Date]: [Product] x[Qty] ([Price] kr)"" or ""- [Product] x[Qty]"".
- Sort chronologically with newest first.
- When showing stock: ""- [Product]: [Qty] pcs""
- Keep responses short and readable. Max 10 lines per category.
- For other questions, answer briefly and directly.";

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

        var clientHistory = history?.ToList() ?? new List<ChatHistoryMessage>();
        clientHistory.Add(new ChatHistoryMessage("user", userMessage));

        var replyText = response.Text ?? "";
        if (!string.IsNullOrEmpty(replyText))
        {
            clientHistory.Add(new ChatHistoryMessage("assistant", replyText));
        }

        return new HomeStoq.Contracts.ChatResponse { Reply = replyText, History = clientHistory };
    }

    public async Task<string?> GenerateShoppingListAsync(string historyJson, string inventoryJson)
    {
        var systemPrompt =
            _language == "Swedish"
                ? $@"You are a predictive pantry assistant. ALL communication, reasoning, and output MUST be strictly in Swedish. 
Identify items that are likely to run out soon.
Respond ONLY with a JSON array.
Format: [ {{ ""ItemName"": ""Mjölk"", ""Quantity"": 2, ""Reason"": ""Konsumerar 2 per vecka, nuvarande lager 0"" }} ]"
                : $@"You are a predictive pantry assistant. Analyze consumption data.
Identify items that are likely to run out soon.
Respond ONLY with a JSON array.
Format: [ {{ ""ItemName"": ""Milk"", ""Quantity"": 2, ""Reason"": ""Consumes 2 per week, stock 0"" }} ]";

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
