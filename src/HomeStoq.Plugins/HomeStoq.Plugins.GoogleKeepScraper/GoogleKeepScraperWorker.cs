using System.Net.Http.Json;
using Microsoft.Playwright;
using HomeStoq.Contracts;

namespace HomeStoq.Plugins.GoogleKeepScraper;

public class GoogleKeepScraperWorker : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleKeepScraperWorker> _logger;
    private readonly Random _random = Random.Shared;

    private IPlaywright? _playwright;
    private IBrowserContext? _browserContext;
    private IPage? _page;

    private readonly string[] _listNames;
    private readonly string _apiUrl;
    private readonly string _profileDir;
    private readonly int _pollIntervalSeconds;
    private readonly int _pollIntervalJitterSeconds;
    private readonly int _activeHoursStart;
    private readonly int _activeHoursEnd;

    private bool _isOnKeepPage;

    public GoogleKeepScraperWorker(
        IConfiguration config,
        ILogger<GoogleKeepScraperWorker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var listNamesConfig = config["Voice:KeepListName"] ?? "inköpslistan";
        _listNames = listNamesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        _apiUrl = config["API:BaseUrl"] ?? "http://localhost:5000/api/voice/command";
        _profileDir = Path.GetFullPath("browser-profile");
        _pollIntervalSeconds = int.Parse(config["Scraper:PollIntervalSeconds"] ?? "45");
        _pollIntervalJitterSeconds = int.Parse(config["Scraper:PollIntervalJitterSeconds"] ?? "15");

        var activeHours = config["Scraper:ActiveHours"] ?? "07-23";
        var parts = activeHours.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end))
        {
            _activeHoursStart = start;
            _activeHoursEnd = end;
        }
        else
        {
            _activeHoursStart = 0;
            _activeHoursEnd = 24;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting GoogleKeepScraper...");

        _playwright = await Playwright.CreateAsync();

        await EnsureBrowserContextAsync();

        _logger.LogInformation("GoogleKeepScraper initialized. Monitoring lists: {ListNames}", string.Join(", ", _listNames));

        await base.StartAsync(cancellationToken);
    }

    private async Task EnsureBrowserContextAsync()
    {
        Directory.CreateDirectory(_profileDir);

        _browserContext = await _playwright!.Chromium.LaunchPersistentContextAsync(_profileDir, new()
        {
            Headless = false,
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
                "--disable-features=Translate",
                "--disable-dev-shm-usage",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-infobars",
                "--window-size=1280,720",
                "--lang=en-US,en",
            },
            Locale = "en-US",
            TimezoneId = "America/New_York",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
        });

        await _browserContext.AddInitScriptAsync(@"
            // Override navigator.webdriver
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });

            // WebGL Stealth
            const getParameter = WebGLRenderingContext.prototype.getParameter;
            WebGLRenderingContext.prototype.getParameter = function(parameter) {
                // UNMASKED_VENDOR_WEBGL
                if (parameter === 37445) return 'Google Inc. (Intel)';
                // UNMASKED_RENDERER_WEBGL
                if (parameter === 37446) return 'ANGLE (Intel, Intel(R) UHD Graphics 620 (0x00003E92) Direct3D11 vs_5_0 ps_5_0, D3D11-27.20.100.8681)';
                return getParameter.apply(this, arguments);
            };

            // Mock plugins to look like a real Chrome installation
            Object.defineProperty(navigator, 'plugins', {
                get: () => {
                    const p = [
                        { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer' },
                        { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai' },
                        { name: 'Native Client', filename: 'internal-nacl-plugin' },
                    ];
                    p.item = (i) => p[i];
                    p.namedItem = (n) => p.find(x => x.name === n);
                    return p;
                }
            });

            // Mock chrome object
            window.chrome = {
                app: { isInstalled: false, InstallState: { DISABLED: 'disabled', INSTALLED: 'installed', NOT_INSTALLED: 'not_installed' }, getDetails: () => null, getIsInstalled: () => false },
                loadTimes: () => ({
                    requestTime: Date.now() / 1000 - 0.5,
                    startLoadTime: Date.now() / 1000 - 0.5,
                    commitLoadTime: Date.now() / 1000 - 0.4,
                    finishDocumentLoadTime: Date.now() / 1000 - 0.3,
                    finishLoadTime: Date.now() / 1000 - 0.2,
                    firstPaintTime: Date.now() / 1000 - 0.35,
                    firstPaintAfterLoadTime: 0,
                    navigationType: 'Other',
                    wasFetchedFromCache: false,
                    wasAlternateProtocolAvailable: false,
                    wasConnectionKeepAlive: true
                }),
                csi: () => ({ startE: Date.now(), onloadT: Date.now() + 200, pageT: 500, tran: 15 }),
                runtime: { OnInstalledReason: { CHROME_UPDATE: 'chrome_update', INSTALL: 'install', SHARED_MODULE_UPDATE: 'shared_module_update', UPDATE: 'update' }, OnRestartRequiredReason: { APP_UPDATE: 'app_update', OS_UPDATE: 'os_update', PERIODIC: 'periodic' }, PlatformArch: { ARM: 'arm', ARM64: 'arm64', MIPS: 'mips', MIPS64: 'mips64', X86_32: 'x86-32', X86_64: 'x86-64' }, PlatformNaclArch: { ARM: 'arm', MIPS: 'mips', MIPS64: 'mips64', X86_32: 'x86-32', X86_64: 'x86-64' }, PlatformOs: { ANDROID: 'android', CROS: 'cros', LINUX: 'linux', MAC: 'mac', OPENBSD: 'openbsd', WIN: 'win' }, RequestUpdateCheckStatus: { NO_UPDATE: 'no_update', THROTTLED: 'throttled', UPDATE_AVAILABLE: 'update_available' } }
            };

            // Override permissions query
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) =>
                parameters.name === 'notifications'
                    ? Promise.resolve({ state: Notification.permission })
                    : originalQuery(parameters);

            // Consistency fixes
            Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
            Object.defineProperty(navigator, 'connection', { get: () => ({ effectiveType: '4g', rtt: 50, downlink: 10, saveData: false }) });
            Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });
            Object.defineProperty(navigator, 'deviceMemory', { get: () => 8 });
            Object.defineProperty(navigator, 'maxTouchPoints', { get: () => 0 });

            // Patch iframe contentWindow
            const originalContentWindow = Object.getOwnPropertyDescriptor(HTMLIFrameElement.prototype, 'contentWindow');
            Object.defineProperty(HTMLIFrameElement.prototype, 'contentWindow', {
                get: function() {
                    const win = originalContentWindow.get.call(this);
                    if (win && win.navigator) {
                        Object.defineProperty(win.navigator, 'webdriver', { get: () => undefined });
                    }
                    return win;
                }
            });
        ");

        _page = await _browserContext.NewPageAsync();
    }

    private bool IsLoggedIn()
    {
        var url = _page?.Url ?? "";
        return !url.Contains("accounts.google.com")
            && !url.Contains("ServiceLogin")
            && !string.IsNullOrEmpty(url);
    }

    private async Task<bool> EnsureSessionAsync()
    {
        if (_page == null)
            return false;

        await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
        _isOnKeepPage = true;

        if (IsLoggedIn())
        {
            _logger.LogInformation("Existing session found on Keep");
            return true;
        }

        _logger.LogInformation("No saved session found.");
        _logger.LogInformation("Please log in to Google Keep in the browser window that just opened.");
        _logger.LogInformation("The scraper will detect your session and start monitoring automatically.");

        for (var i = 0; i < 600; i++)
        {
            await DelayAsync(3000);
            if (IsLoggedIn())
            {
                _logger.LogInformation("Login detected! Session saved.");
                return true;
            }
        }

        _logger.LogError("Login timed out after 30 minutes");
        return false;
    }

    private async Task WaitForKeepLoadedAsync()
    {
        if (_page == null)
            return;

        for (var i = 0; i < 15; i++)
        {
            var sidebar = _page.Locator("nav").First;
            var checkboxes = _page.GetByRole(AriaRole.Checkbox).First;
            
            if (await sidebar.IsVisibleAsync() || await checkboxes.IsVisibleAsync())
                return;

            _logger.LogDebug("Waiting for Keep to load... (attempt {Attempt})", i + 1);
            await DelayAsync(2000);
        }

        _logger.LogWarning("Keep did not load within timeout");
        await TakeScreenshotAsync("keep-not-loaded.png");
    }

    private async Task HumanDelayAsync(int baseMs, int jitterMs = 500)
    {
        var delay = baseMs + _random.Next(-jitterMs, jitterMs);
        delay = Math.Max(100, delay);
        await Task.Delay(delay);
    }

    private async Task DelayAsync(int baseMs)
    {
        var jitter = (int)(baseMs * 0.15);
        var delay = baseMs + _random.Next(-jitter, jitter);
        delay = Math.Max(100, delay);
        await Task.Delay(delay);
    }

    private async Task PollDelayAsync(CancellationToken token)
    {
        var delay = (_pollIntervalSeconds + _random.Next(-_pollIntervalJitterSeconds, _pollIntervalJitterSeconds)) * 1000;
        delay = Math.Max(10000, delay);
        await Task.Delay(delay, token);
    }

    private async Task MoveMouseToElementAsync(ILocator element)
    {
        if (_page == null) return;

        var box = await element.BoundingBoxAsync();
        if (box == null) return;

        var startX = _random.Next(100, 300);
        var startY = _random.Next(100, 300);
        await _page.Mouse.MoveAsync(startX, startY);

        var steps = _random.Next(8, 15);
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var ease = t * t * (3 - 2 * t);
            var x = startX + (box.X + box.Width / 2 - startX) * ease + _random.Next(-5, 5);
            var y = startY + (box.Y + box.Height / 2 - startY) * ease + _random.Next(-5, 5);
            await _page.Mouse.MoveAsync((float)x, (float)y);
            await Task.Delay(_random.Next(8, 25));
        }

        await _page.Mouse.MoveAsync((float)(box.X + box.Width / 2), (float)(box.Y + box.Height / 2));
        await Task.Delay(_random.Next(100, 400));
    }

    private bool IsWithinActiveHours()
    {
        var now = DateTime.Now.Hour;
        if (_activeHoursStart == _activeHoursEnd) return true;
        if (_activeHoursStart < _activeHoursEnd)
        {
            return now >= _activeHoursStart && now < _activeHoursEnd;
        }
        else
        {
            // Handles cases like 23-07
            return now >= _activeHoursStart || now < _activeHoursEnd;
        }
    }

    private async Task PerformRandomActivityAsync()
    {
        if (_page == null) return;

        // 10% chance to do something random
        if (_random.Next(0, 100) > 10) return;

        _logger.LogInformation("Performing random behavioral activity...");
        var choice = _random.Next(0, 3);

        switch (choice)
        {
            case 0:
                // Navigate to Reminders and back
                _logger.LogDebug("  Navigating to Reminders tab...");
                var reminders = _page.GetByText("Reminders").Or(_page.GetByText("Påminnelser")).First;
                if (await reminders.IsVisibleAsync()) await reminders.ClickAsync();
                await HumanDelayAsync(2000, 1000);
                var notes = _page.GetByText("Notes").Or(_page.GetByText("Anteckningar")).First;
                if (await notes.IsVisibleAsync()) await notes.ClickAsync();
                break;
            case 1:
                // Just scroll a bit
                _logger.LogDebug("  Scrolling page...");
                await _page.Mouse.WheelAsync(0, _random.Next(100, 500));
                await HumanDelayAsync(1500, 500);
                await _page.Mouse.WheelAsync(0, -_random.Next(100, 500));
                break;
            case 2:
                // Hover over random notes
                _logger.LogDebug("  Hovering over random notes...");
                var noteCards = _page.Locator("[role='button'][aria-label]");
                var count = await noteCards.CountAsync();
                if (count > 0)
                {
                    var index = _random.Next(0, Math.Min(count, 5));
                    await MoveMouseToElementAsync(noteCards.Nth(index));
                    await HumanDelayAsync(800, 300);
                }
                break;
        }
        
        await HumanDelayAsync(1000, 500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sessionReady = false;

        while (!stoppingToken.IsCancellationRequested && !sessionReady)
        {
            try
            {
                if (!IsWithinActiveHours())
                {
                    _logger.LogInformation("Outside active hours ({Start}-{End}). Sleeping for 30 minutes...", _activeHoursStart, _activeHoursEnd);
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    continue;
                }

                sessionReady = await EnsureSessionAsync();
                if (!sessionReady)
                {
                    _logger.LogWarning("Session not ready. Retrying in 10 seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error establishing session");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        await WaitForKeepLoadedAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!IsWithinActiveHours())
                {
                    _logger.LogInformation("Entering sleep period until active hours ({Start}-{End})...", _activeHoursStart, _activeHoursEnd);
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    continue;
                }

                await PerformRandomActivityAsync();
                await ProcessListsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Keep lists");

                try
                {
                    await RecoverAsync();
                }
                catch (Exception recoverEx)
                {
                    _logger.LogError(recoverEx, "Recovery failed");
                }
            }

            await PollDelayAsync(stoppingToken);
        }
    }

    private async Task ProcessListsAsync()
    {
        if (_page == null)
            return;

        if (!_isOnKeepPage || !IsLoggedIn())
        {
            await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
            _isOnKeepPage = true;
            await HumanDelayAsync(1500, 500);

            if (!IsLoggedIn())
            {
                _logger.LogWarning("Session expired. Please log in again in the browser window.");
                _isOnKeepPage = false;
                await EnsureSessionAsync();
                await WaitForKeepLoadedAsync();
                return;
            }
        }
        else
        {
            await _page.ReloadAsync(new() { WaitUntil = WaitUntilState.Load });
            await HumanDelayAsync(1000, 300);
        }

        foreach (var listName in _listNames)
        {
            try
            {
                await ProcessSingleListAsync(listName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing list '{ListName}'", listName);
            }
        }
    }

    private async Task ProcessSingleListAsync(string listName)
    {
        if (_page == null)
            return;

        _logger.LogDebug("Searching for list '{ListName}'...", listName);

        // 1. Try to find the note card by its title in the main view
        // Google Keep titles are often role="textbox" with contenteditable="false" OR just divs with text.
        // We'll search for the text and look for the surrounding note card.
        var titleElement = _page.GetByText(listName, new() { Exact = true }).First;
        ILocator? listButton = null;

        if (await titleElement.IsVisibleAsync())
        {
            // The card itself is usually a parent div with role="button" or a specific class
            listButton = titleElement;
            _logger.LogDebug("Found title element for '{ListName}'", listName);
        }
        else
        {
            // 2. Fallback: Try sidebar navigation (Labels)
            var sidebar = _page.Locator("nav").First;
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
        await MoveMouseToElementAsync(listButton);
        await listButton.ClickAsync(new() { Force = true });
        
        // Wait for expanded view - typically has a "Done" or "Close" button
        // and a specific role for the dialog/modal
        await HumanDelayAsync(1500, 500);

        // 4. Process checkboxes (limit to the expanded note area if possible)
        // In expanded view, the note is usually inside a div with role="dialog" or similar
        var container = _page.Locator("[role='dialog']").First;
        if (!await container.IsVisibleAsync())
        {
            _logger.LogWarning("Expanded note container not found for '{ListName}', trying global checkboxes", listName);
            container = _page.Locator("body");
        }

        var allCheckboxes = container.GetByRole(AriaRole.Checkbox);
        var totalCount = await allCheckboxes.CountAsync();

        if (totalCount == 0)
        {
            _logger.LogDebug("No checkboxes found in '{ListName}'", listName);
            await CloseExpandedNoteAsync();
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

            // Get the parent list item to extract text
            // Structure is often Checkbox -> Label/Div -> Text
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
                    new VoiceCommandRequest(text));

                if (response.IsSuccessStatusCode)
                {
                    await MoveMouseToElementAsync(checkbox);
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
        await DeleteTickedItemsAsync(container);

        // 6. Close the expanded note
        await CloseExpandedNoteAsync();
    }

    private async Task DeleteTickedItemsAsync(ILocator container)
    {
        if (_page == null) return;

        // 1. Find the "More" (3-dot) menu button inside the dialog
        // We check for both English ("More") and Swedish ("Mer") aria-labels
        var moreButton = container.GetByRole(AriaRole.Button, new() { Name = "More" })
            .Or(container.GetByRole(AriaRole.Button, new() { Name = "Mer" }))
            .First;
        
        if (await moreButton.IsVisibleAsync())
        {
            _logger.LogDebug("Clicking 'More' menu to clean up items...");
            await MoveMouseToElementAsync(moreButton);
            await moreButton.ClickAsync();
            await HumanDelayAsync(600, 200);

            // 2. Find the "Delete ticked items" option in the global menu
            // Google Keep menus are usually appended to the body.
            var deleteOption = _page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete ticked items" })
                .Or(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Ta bort markerade objekt" }))
                .First;

            if (await deleteOption.IsVisibleAsync())
            {
                await deleteOption.ClickAsync();
                _logger.LogInformation("  Cleaned up completed items from the list.");
                await HumanDelayAsync(1200, 400); // Wait for deletion animation
            }
            else
            {
                _logger.LogDebug("  'Delete ticked items' option not found in menu.");
                // Close the menu if we opened it but didn't find the option
                await _page.Keyboard.PressAsync("Escape");
                await HumanDelayAsync(300, 100);
            }
        }
        else
        {
            _logger.LogWarning("  'More' menu button not found in note container.");
        }
    }

    private async Task CloseExpandedNoteAsync()
    {
        if (_page == null) return;

        var doneButton = _page.GetByText("Done", new() { Exact = true }).First;
        if (!await doneButton.IsVisibleAsync())
        {
            doneButton = _page.GetByText("Stäng", new() { Exact = true }).First; // Swedish fallback
        }

        if (await doneButton.IsVisibleAsync())
        {
            await doneButton.ClickAsync();
            _logger.LogDebug("Closed expanded note.");
            await HumanDelayAsync(500, 200);
        }
        else
        {
            // Click outside or press Esc
            await _page.Keyboard.PressAsync("Escape");
            _logger.LogDebug("Pressed Escape to close note.");
            await HumanDelayAsync(500, 200);
        }
    }

    private async Task RecoverAsync()
    {
        _logger.LogInformation("Attempting recovery...");

        try
        {
            if (_page != null)
            {
                await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
                _isOnKeepPage = true;
                await HumanDelayAsync(2000, 500);

                if (IsLoggedIn())
                {
                    _logger.LogInformation("Recovery successful, session still valid");
                    return;
                }
            }

            _logger.LogInformation("Session expired. Please log in again in the browser window.");
            _isOnKeepPage = false;
            await EnsureSessionAsync();
            await WaitForKeepLoadedAsync();
        }
        catch
        {
            _logger.LogWarning("Recovery via page navigation failed. Recreating browser context...");

            if (_browserContext != null)
                await _browserContext.CloseAsync();

            await EnsureBrowserContextAsync();
            await EnsureSessionAsync();
            await WaitForKeepLoadedAsync();
        }
    }

    private async Task TakeScreenshotAsync(string filename)
    {
        try
        {
            var path = Path.Combine(_profileDir, filename);
            await _page!.ScreenshotAsync(new() { Path = path });
            _logger.LogInformation("Screenshot saved to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take screenshot");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping GoogleKeepScraper...");

        try
        {
            if (_browserContext != null)
                await _browserContext.CloseAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing browser context");
        }

        _playwright?.Dispose();
        _httpClient.Dispose();

        await base.StopAsync(cancellationToken);
    }
}
