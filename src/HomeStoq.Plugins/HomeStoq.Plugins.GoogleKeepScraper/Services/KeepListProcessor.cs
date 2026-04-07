using Microsoft.Playwright;
using HomeStoq.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public class KeepListProcessor : IKeepListProcessor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeepListProcessor> _logger;
    private readonly string _apiUrl;

    public KeepListProcessor(HttpClient httpClient, ILogger<KeepListProcessor> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Use explicit BaseUrl if configured, otherwise derive from HostUrl
        var explicitBaseUrl = config["API:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(explicitBaseUrl))
        {
            _apiUrl = explicitBaseUrl;
            _logger.LogDebug("Using explicit API BaseUrl: {Url}", _apiUrl);
        }
        else
        {
            // Derive from HostUrl: replace * with localhost and append /api/voice/command
            var hostUrl = config["App:HostUrl"] ?? "http://*:5050";
            var apiBase = hostUrl.Replace("*", "localhost").TrimEnd('/');
            _apiUrl = $"{apiBase}/api/voice/command";
            _logger.LogDebug("Derived API URL from HostUrl: {Url}", _apiUrl);
        }
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
            var sidebar = page.GetByRole(AriaRole.Navigation).First;
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
        await BrowserUtils.MoveMouseToElementAsync(page, listButton);
        await listButton.ClickAsync(new() { Force = true });
        
        await BrowserUtils.HumanDelayAsync(1500, 500);

        // 4. Process checkboxes
        var container = page.GetByRole(AriaRole.Dialog).First;
        if (!await container.IsVisibleAsync())
        {
            _logger.LogWarning("Expanded note container not found for '{ListName}', trying global checkboxes", listName);
            container = page.Locator("body");
        }

        const int maxIterations = 50;
        var iterations = 0;
        var processedCount = 0;

        while (iterations < maxIterations)
        {
            iterations++;

            var checkboxes = await container.GetByRole(AriaRole.Checkbox).AllAsync();

            int uncheckedIndex = -1;
            for (var i = 0; i < checkboxes.Count; i++)
            {
                if (!await checkboxes[i].IsCheckedAsync())
                {
                    uncheckedIndex = i;
                    break;
                }
            }

            if (uncheckedIndex == -1)
                break;

            var checkbox = container.GetByRole(AriaRole.Checkbox).Nth(uncheckedIndex);
            var listItem = checkbox.Locator("..").Locator("..");
            var text = (await listItem.InnerTextAsync())?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Skipping item at index {Index} with empty text", uncheckedIndex);
                continue;
            }

            _logger.LogInformation("Processing: {Text}", text);

            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    _apiUrl,
                    new VoiceCommandRequestDto(text));

                if (response.IsSuccessStatusCode)
                {
                    await BrowserUtils.MoveMouseToElementAsync(page, checkbox);
                    await checkbox.ClickAsync();
                    await BrowserUtils.HumanDelayAsync(800, 300);
                    _logger.LogInformation("  Processed and checked: {Text}", text);
                    processedCount++;
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

        if (iterations >= maxIterations)
        {
            _logger.LogWarning("Max iterations ({Max}) reached, stopping to prevent infinite loop", maxIterations);
        }

        if (processedCount == 0)
        {
            _logger.LogDebug("No unchecked items in '{ListName}'", listName);
        }
        else
        {
            _logger.LogInformation("Processed {Count} unchecked item(s) in '{ListName}'", processedCount, listName);
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
            await BrowserUtils.MoveMouseToElementAsync(page, moreButton);
            await moreButton.ClickAsync();
            await BrowserUtils.HumanDelayAsync(600, 200);

            var deleteOption = page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete ticked items" })
                .Or(page.GetByRole(AriaRole.Menuitem, new() { Name = "Ta bort markerade objekt" }))
                .First;

            if (await deleteOption.IsVisibleAsync())
            {
                await deleteOption.ClickAsync();
                _logger.LogInformation("  Cleaned up completed items from the list.");
                await BrowserUtils.HumanDelayAsync(1200, 400);
            }
            else
            {
                _logger.LogDebug("  'Delete ticked items' option not found in menu.");
                await page.Keyboard.PressAsync("Escape");
                await BrowserUtils.HumanDelayAsync(300, 100);
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
            await BrowserUtils.HumanDelayAsync(500, 200);
        }
        else
        {
            await page.Keyboard.PressAsync("Escape");
            _logger.LogDebug("Pressed Escape to close note.");
            await BrowserUtils.HumanDelayAsync(500, 200);
        }
    }
}
