using Microsoft.Playwright;
using HomeStoq.Contracts;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public class KeepListProcessor : IKeepListProcessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeepListProcessor> _logger;
    private readonly Random _random = Random.Shared;
    private readonly string _apiUrl;

    public KeepListProcessor(HttpClient httpClient, ILogger<KeepListProcessor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiUrl = "http://localhost:5000/api/voice/command";
    }

    public async Task ProcessListsAsync(IPage page, string[] listNames)
    {
        foreach (var listName in listNames)
        {
            try
            {
                await ProcessSingleListAsync(page, listName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing list '{ListName}'", listName);
            }
        }
    }

    private async Task ProcessSingleListAsync(IPage page, string listName)
    {
        _logger.LogDebug("Searching for list '{ListName}'...", listName);

        // 1. Try to find the note card by its title in the main view
        var titleElement = page.GetByText(listName, new() { Exact = true }).First;
        ILocator? listButton = null;

        if (await titleElement.IsVisibleAsync())
        {
            listButton = titleElement;
            _logger.LogDebug("Found title element for '{ListName}'", listName);
        }
        else
        {
            // 2. Fallback: Try sidebar navigation (Labels)
            var sidebar = page.Locator("nav").First;
            var sidebarLabel = sidebar.GetByText(listName, new() { Exact = true }).First;
            if (await sidebarLabel.IsVisibleAsync())
            {
                listButton = sidebarLabel;
                _logger.LogDebug("Found sidebar label for '{ListName}'", listName);
            }
        }

        if (listButton == null || !await listButton.IsVisibleAsync())
        {
            _logger.LogWarning("List '{ListName}' not found (tried main view and sidebar)", listName);
            return;
        }

        // 3. Click to open/expand the note
        _logger.LogDebug("Opening list '{ListName}'...", listName);
        await MoveMouseToElementAsync(page, listButton);
        await listButton.ClickAsync(new() { Force = true });
        
        await HumanDelayAsync(1500, 500);

        // 4. Process checkboxes
        var container = page.Locator("[role='dialog']").First;
        if (!await container.IsVisibleAsync())
        {
            _logger.LogWarning("Expanded note container not found for '{ListName}', trying global checkboxes", listName);
            container = page.Locator("body");
        }

        var allCheckboxes = container.GetByRole(AriaRole.Checkbox);
        var totalCount = await allCheckboxes.CountAsync();

        if (totalCount == 0)
        {
            _logger.LogDebug("No checkboxes found in '{ListName}'", listName);
            await CloseExpandedNoteAsync(page);
            return;
        }

        var uncheckedCount = 0;
        for (var i = 0; i < totalCount; i++)
        {
            var checkbox = allCheckboxes.Nth(i);
            var isChecked = await checkbox.IsCheckedAsync();

            if (isChecked)
                continue;

            uncheckedCount++;

            var listItem = checkbox.Locator("..").Locator("..");
            var text = await listItem.InnerTextAsync();

            if (string.IsNullOrWhiteSpace(text))
            {
                var html = await listItem.InnerHTMLAsync();
                _logger.LogDebug("Item {Index}: HTML='{Html}'", i, html[..Math.Min(200, html.Length)]);
                _logger.LogWarning("Skipping item {Index} with empty text", i);
                continue;
            }

            text = text.Trim();
            _logger.LogInformation("Processing: {Text}", text);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    _apiUrl,
                    new VoiceCommandRequestDto(text));

                if (response.IsSuccessStatusCode)
                {
                    await MoveMouseToElementAsync(page, checkbox);
                    await checkbox.ClickAsync();
                    await HumanDelayAsync(800, 300);
                    _logger.LogInformation("  Processed and checked: {Text}", text);
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("  API returned {Status}: {Body}", response.StatusCode, body[..Math.Min(200, body.Length)]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "  Error calling API for item: {Text}", text);
            }
        }

        if (uncheckedCount == 0)
        {
            _logger.LogDebug("No unchecked items in '{ListName}'", listName);
        }
        else
        {
            _logger.LogInformation("Processed {Count} unchecked item(s) in '{ListName}'", uncheckedCount, listName);
        }

        // 5. Delete ticked items to keep the list clean
        await DeleteTickedItemsAsync(page, container);

        // 6. Close the expanded note
        await CloseExpandedNoteAsync(page);
    }

    private async Task DeleteTickedItemsAsync(IPage page, ILocator container)
    {
        var moreButton = container.GetByRole(AriaRole.Button, new() { Name = "More" })
            .Or(container.GetByRole(AriaRole.Button, new() { Name = "Mer" }))
            .First;
        
        if (await moreButton.IsVisibleAsync())
        {
            _logger.LogDebug("Clicking 'More' menu to clean up items...");
            await MoveMouseToElementAsync(page, moreButton);
            await moreButton.ClickAsync();
            await HumanDelayAsync(600, 200);

            var deleteOption = page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete ticked items" })
                .Or(page.GetByRole(AriaRole.Menuitem, new() { Name = "Ta bort markerade objekt" }))
                .First;

            if (await deleteOption.IsVisibleAsync())
            {
                await deleteOption.ClickAsync();
                _logger.LogInformation("  Cleaned up completed items from the list.");
                await HumanDelayAsync(1200, 400);
            }
            else
            {
                _logger.LogDebug("  'Delete ticked items' option not found in menu.");
                await page.Keyboard.PressAsync("Escape");
                await HumanDelayAsync(300, 100);
            }
        }
        else
        {
            _logger.LogWarning("  'More' menu button not found in note container.");
        }
    }

    private async Task CloseExpandedNoteAsync(IPage page)
    {
        var doneButton = page.GetByText("Done", new() { Exact = true }).First;
        if (!await doneButton.IsVisibleAsync())
        {
            doneButton = page.GetByText("Stäng", new() { Exact = true }).First;
        }

        if (await doneButton.IsVisibleAsync())
        {
            await doneButton.ClickAsync();
            _logger.LogDebug("Closed expanded note.");
            await HumanDelayAsync(500, 200);
        }
        else
        {
            await page.Keyboard.PressAsync("Escape");
            _logger.LogDebug("Pressed Escape to close note.");
            await HumanDelayAsync(500, 200);
        }
    }

    private async Task HumanDelayAsync(int baseMs, int jitterMs = 500)
    {
        var delay = baseMs + _random.Next(-jitterMs, jitterMs);
        delay = Math.Max(100, delay);
        await Task.Delay(delay);
    }

    private async Task MoveMouseToElementAsync(IPage page, ILocator element)
    {
        var box = await element.BoundingBoxAsync();
        if (box == null) return;

        var startX = _random.Next(100, 300);
        var startY = _random.Next(100, 300);
        await page.Mouse.MoveAsync(startX, startY);

        var steps = _random.Next(8, 15);
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var ease = t * t * (3 - 2 * t);
            var x = startX + (box.X + box.Width / 2 - startX) * ease + _random.Next(-5, 5);
            var y = startY + (box.Y + box.Height / 2 - startY) * ease + _random.Next(-5, 5);
            await page.Mouse.MoveAsync((float)x, (float)y);
            await Task.Delay(_random.Next(8, 25));
        }

        await page.Mouse.MoveAsync((float)(box.X + box.Width / 2), (float)(box.Y + box.Height / 2));
        await Task.Delay(_random.Next(100, 400));
    }
}
