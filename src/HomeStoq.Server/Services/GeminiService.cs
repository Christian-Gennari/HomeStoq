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
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey =
            configuration["GEMINI_API_KEY"]
            ?? throw new ArgumentNullException("GEMINI_API_KEY not configured");
        _model = configuration["GEMINI_MODEL"] ?? "gemini-3.1-flash-lite-preview";
    }

    private string GeminiEndpoint => $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

    public async Task<ParsedVoiceAction?> ParseVoiceCommandAsync(string text)
    {
        var prompt =
            $@"You are a system that parses food inventory voice commands.
            Analyze the following text and determine the item and the action (Add or Remove) and quantity.
            Input: ""{text}""
            Respond ONLY with a JSON object in this format:
            {{ ""ItemName"": ""Milk"", ""Action"": ""Remove"", ""Quantity"": 1 }}
            If you cannot determine the action or item, return null.";

        var response = await CallGeminiAsync(prompt);
        if (string.IsNullOrEmpty(response) || response.Equals("null", StringComparison.OrdinalIgnoreCase))
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
            _logger.LogError(ex, "Failed to deserialize Gemini response for voice command: {Response}", response);
            return null;
        }
    }

    public async Task<List<PantryItem>?> ProcessReceiptImageAsync(
        byte[] imageBytes,
        string mimeType = "image/jpeg"
    )
    {
        var base64Image = Convert.ToBase64String(imageBytes);
        var prompt =
            @"You are a system that reads grocery receipts.
            Analyze the provided image or document and list all relevant food items with their prices.
            Ignore deposits, plastic bags, discounts, and totals.
            Map items to generic names (e.g. 'Organic Free Range Eggs 12pk' -> 'Eggs').
            Extract the price for each item if available (e.g. '2.99').
            Respond ONLY with a JSON array in this format:
            [ { ""ItemName"": ""Eggs"", ""Quantity"": 1, ""Price"": 2.99 }, { ""ItemName"": ""Milk"", ""Quantity"": 2, ""Price"": null } ]";

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
            _logger.LogError("Gemini API error (Receipt): {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var textResponse = json?.Candidates?[0]?.Content?.Parts?[0]?.Text;

        if (string.IsNullOrEmpty(textResponse))
            return null;

        // Clean JSON formatting if Gemini wraps it in code blocks
        var cleaned = textResponse.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        try
        {
            return JsonSerializer.Deserialize<List<PantryItem>>(
                cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Gemini response for receipt scan: {Response}", cleaned);
            return null;
        }
    }

    public async Task<string?> GenerateShoppingListAsync(string historyJson, string inventoryJson)
    {
        var prompt =
            $@"You are a predictive pantry assistant.
            Analyze the following historical consumption data (History) and current inventory (Inventory).
            Identify items that are likely to run out soon or are already low based on patterns.
            History: {historyJson}
            Inventory: {inventoryJson}
            Respond ONLY with a JSON array of suggested items to buy, including estimated quantity and reason.
            Format: [ {{ ""ItemName"": ""Milk"", ""Quantity"": 2, ""Reason"": ""Consumes 2 per week, current stock 0"" }} ]";

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
            _logger.LogError("Gemini API error: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var textResponse = json?.Candidates?[0]?.Content?.Parts?[0]?.Text;

        if (string.IsNullOrEmpty(textResponse))
            return null;

        var cleaned = textResponse.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        return cleaned;
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
    public string Action { get; set; } = string.Empty; // "Add" or "Remove"
    public double Quantity { get; set; }
}

public class PantryItem
{
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double? Price { get; set; }
}
