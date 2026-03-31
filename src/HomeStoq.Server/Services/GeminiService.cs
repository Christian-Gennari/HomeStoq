using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeStoq.Server.Models;

namespace HomeStoq.Server.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _language;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiService> logger
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey =
            configuration["GEMINI_API_KEY"]
            ?? throw new InvalidOperationException("GEMINI_API_KEY not configured");
        _model = configuration["GEMINI_MODEL"] ?? "gemini-3.1-flash-lite-preview";
        _language = NormalizeLanguage(configuration["App:Language"]);
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.Equals(language, "Swedish", StringComparison.OrdinalIgnoreCase))
            return "Swedish";
        if (string.Equals(language, "English", StringComparison.OrdinalIgnoreCase))
            return "English";
        return "English";
    }

    private string GeminiEndpoint =>
        $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

    public async Task<ParsedVoiceAction?> ParseVoiceCommandAsync(
        string text,
        IEnumerable<string>? existingItems = null
    )
    {
        var inventoryContext =
            existingItems != null && existingItems.Any()
                ? $"Current Inventory Items: {string.Join(", ", existingItems)}"
                : "";

        var prompt = _language switch
        {
            "Swedish" =>
                $@"You are a food inventory assistant. The following voice command is in Swedish. Interpret the intent.

Command: ""{text}""

{inventoryContext}

Guidelines:
1. The command is in Swedish. Return the ItemName in Swedish.
2. Use the inventory list above to match existing item names. If the user says something close to an existing item, use that exact name.
3. Identify ItemName, Action (Add or Remove), and Quantity.
4. Interpret NATURAL language intent. Examples:
   - ""slut på mjölk"" → Remove, Mjölk, 1
   - ""köpte 3 ägg"" → Add, Ägg, 3
   - ""använt allt kaffe"" → Remove, Kaffe, 1
   - ""lägg till bröd"" → Add, Bröd, 1
   - ""ägg är slut"" → Remove, Ägg, 1
   - ""tog 2 fil"" → Remove, Filmjölk, 2
5. Default quantity to 1 if not specified.
6. Respond ONLY with a JSON object. If unsure, return null.

Format: {{ ""ItemName"": ""Mjölk"", ""Action"": ""Remove"", ""Quantity"": 1 }}",

            _ =>
                $@"You are a food inventory assistant. Interpret the intent of this voice command: ""{text}""

{inventoryContext}

Guidelines:
1. The command is in English.
2. Identify ItemName, Action (Add or Remove), and Quantity.
3. Interpret NATURAL language intent. Examples:
   - ""used the last milk"" → Remove, Milk, 1
   - ""bought 3 eggs"" → Add, Eggs, 3
   - ""all the coffee is gone"" → Remove, Coffee, 1
   - ""add bread"" → Add, Bread, 1
   - ""eggs are finished"" → Remove, Eggs, 1
4. If the user mentions an item that sounds like an existing item, use the existing item's name.
5. Default quantity to 1 if not specified.
6. Respond ONLY with a JSON object. If unsure, return null.

Format: {{ ""ItemName"": ""Milk"", ""Action"": ""Remove"", ""Quantity"": 1 }}",
        };

        var response = await CallGeminiAsync(prompt);
        if (
            string.IsNullOrEmpty(response)
            || response.Equals("null", StringComparison.OrdinalIgnoreCase)
        )
            return null;

        try
        {
            return JsonSerializer.Deserialize<ParsedVoiceAction>(
                response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize Gemini response for voice command: {Response}",
                response
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
        var base64Image = Convert.ToBase64String(imageBytes);
        var inventoryContext =
            existingItems != null && existingItems.Any()
                ? $"Current Inventory Items (use these names to match): {string.Join(", ", existingItems)}"
                : "";

        var prompt = _language switch
        {
            "Swedish" =>
                $@"You are a system that reads Swedish grocery receipts. Analyze the provided image or document and extract all food and household items with their prices.

{inventoryContext}

RULES:
1. Return ALL item names in SWEDISH. Never translate to English.
2. Keep brand names and product names exactly as they appear on the receipt (e.g., ""Gammaldags Idealmakaroner"", ""Risifrutti"", ""Mannafrutti"", ""Eldorado"").
3. Map generic English-sounding categories to Swedish:
   - ""Dishwasher Tablets"" → ""Diskmaskinstabletter""
   - ""Bread"" → ""Bröd""
   - ""Eggs"" → ""Ägg""
   - ""Cleaning Supplies"" → ""Rengöringsmedel""
   - ""Meat-free Meatballs"" → ""Vegetariska bullar""
   - ""Oat Cream"" → ""Havregrädde""
   - ""Pasta Sauce"" → ""Pastasås""
   - ""Elderflower Drink"" → ""Fläderdryck""
   - ""Vegopirog"" → ""Vegetarisk pirog""
4. Use the inventory list above to match existing item names when applicable.
5. Ignore: deposits (pant), plastic bags (kassar), discounts (rabatt), totals, loyalty points, packaging fees.
6. Extract the price for each item if visible on the receipt.
7. Respond ONLY with a JSON array.

Format: [ {{ ""ItemName"": ""Ägg"", ""Quantity"": 1, ""Price"": 34.90 }}, {{ ""ItemName"": ""Mjölk"", ""Quantity"": 2, ""Price"": null }} ]",

            _ =>
                $@"You are a system that reads grocery receipts. Analyze the provided image or document and extract all food and household items with their prices.

{inventoryContext}

RULES:
1. Return ALL item names in English.
2. Keep brand names as they appear on the receipt.
3. Map items to generic names (e.g., ""Organic Free Range Eggs 12pk"" → ""Eggs"").
4. Use the inventory list above to match existing item names when applicable.
5. Ignore: deposits, bags, discounts, totals, loyalty points, packaging fees.
6. Extract the price for each item if visible.
7. Respond ONLY with a JSON array.

Format: [ {{ ""ItemName"": ""Eggs"", ""Quantity"": 1, ""Price"": 2.99 }}, {{ ""ItemName"": ""Milk"", ""Quantity"": 2, ""Price"": null }} ]",
        };

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new { inlineData = new { mimeType = mimeType, data = base64Image } },
                    },
                },
            },
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{GeminiEndpoint}?key={_apiKey}",
            requestBody
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Gemini API error (Receipt): {StatusCode} - {Error}",
                response.StatusCode,
                error
            );
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var textResponse = json?.Candidates?[0]?.Content?.Parts?[0]?.Text;

        if (string.IsNullOrEmpty(textResponse))
            return null;

        var cleaned = CleanJsonFromMarkdown(textResponse);
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
                "Failed to deserialize Gemini response for receipt scan: {Response}",
                cleaned
            );
            return null;
        }
    }

    public async Task<string?> GenerateShoppingListAsync(string historyJson, string inventoryJson)
    {
        var prompt = _language switch
        {
            "Swedish" =>
                $@"You are a predictive pantry assistant. The inventory and history data below is in Swedish. Analyze it and return suggestions with Swedish item names.

History: {historyJson}
Inventory: {inventoryJson}

Identify items that are likely to run out soon or are already low based on patterns.
Respond ONLY with a JSON array of suggested items to buy, including estimated quantity and reason (in Swedish).
Format: [ {{ ""ItemName"": ""Mjölk"", ""Quantity"": 2, ""Reason"": ""Konsumerar 2 per vecka, nuvarande lager 0"" }} ]",

            _ => $@"You are a predictive pantry assistant.
            Analyze the following historical consumption data (History) and current inventory (Inventory).
            Identify items that are likely to run out soon or are already low based on patterns.
            History: {historyJson}
            Inventory: {inventoryJson}
            Respond ONLY with a JSON array of suggested items to buy, including estimated quantity and reason.
            Format: [ {{ ""ItemName"": ""Milk"", ""Quantity"": 2, ""Reason"": ""Consumes 2 per week, current stock 0"" }} ]",
        };

        return await CallGeminiAsync(prompt);
    }

    private async Task<string?> CallGeminiAsync(string prompt)
    {
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{GeminiEndpoint}?key={_apiKey}",
            requestBody
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Gemini API error: {StatusCode} - {Error}",
                response.StatusCode,
                error
            );
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var textResponse = json?.Candidates?[0]?.Content?.Parts?[0]?.Text;

        return CleanJsonFromMarkdown(textResponse);
    }

    private static string? CleanJsonFromMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var cleaned = text.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned.Substring(3);

        cleaned = cleaned.Trim();
        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        return cleaned.Trim();
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}

public class ParsedVoiceAction
{
    public string ItemName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public double Quantity { get; set; }
}

public class PantryItem
{
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double? Price { get; set; }
}
