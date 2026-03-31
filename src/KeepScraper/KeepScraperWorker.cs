using System.Net.Http.Json;
using Microsoft.Playwright;

namespace HomeStoq.KeepScraper;

public class KeepScraperWorker : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeepScraperWorker> _logger;
    private readonly Random _random = Random.Shared;

    private IPlaywright? _playwright;
    private IBrowserContext? _browserContext;
    private IPage? _page;

    private readonly string _listName;
    private readonly string _apiUrl;
    private readonly string _profileDir;
    private readonly int _pollIntervalSeconds;
    private readonly int _pollIntervalJitterSeconds;

    private bool _isOnKeepPage;

    public KeepScraperWorker(
        IConfiguration config,
        ILogger<KeepScraperWorker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        _listName = config["KEEP_LIST_NAME"] ?? "inköpslistan";
        _apiUrl = config["HOMESTOQ_API_URL"] ?? "http://localhost:5188/api/voice/command";
        _profileDir = Path.GetFullPath(config["BROWSER_PROFILE_DIR"] ?? "browser-profile");
        _pollIntervalSeconds = int.Parse(config["POLL_INTERVAL_SECONDS"] ?? "45");
        _pollIntervalJitterSeconds = int.Parse(config["POLL_INTERVAL_JITTER_SECONDS"] ?? "15");
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting KeepScraper...");

        _playwright = await Playwright.CreateAsync();

        await EnsureBrowserContextAsync();

        _logger.LogInformation("KeepScraper initialized. Monitoring list: {ListName}", _listName);

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

            // Mock plugins to look like a real Chrome installation
            Object.defineProperty(navigator, 'plugins', {
                get: () => [
                    { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer' },
                    { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai' },
                    { name: 'Native Client', filename: 'internal-nacl-plugin' },
                ]
            });

            // Mock languages
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-US', 'en']
            });

            // Override permissions query
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) =>
                parameters.name === 'notifications'
                    ? Promise.resolve({ state: Notification.permission })
                    : originalQuery(parameters);

            // Override chrome runtime
            window.chrome = window.chrome || {};
            window.chrome.runtime = window.chrome.runtime || {};

            // Override connection information
            Object.defineProperty(navigator, 'connection', {
                get: () => ({ effectiveType: '4g', rtt: 50, downlink: 10, saveData: false })
            });

            // Override hardware concurrency
            Object.defineProperty(navigator, 'hardwareConcurrency', {
                get: () => 8
            });

            // Override device memory
            Object.defineProperty(navigator, 'deviceMemory', {
                get: () => 8
            });

            // Override maxTouchPoints
            Object.defineProperty(navigator, 'maxTouchPoints', {
                get: () => 0
            });

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

        for (var i = 0; i < 10; i++)
        {
            var sidebar = _page.Locator("nav").First;
            if (await sidebar.IsVisibleAsync())
                return;

            _logger.LogDebug("Waiting for Keep sidebar to load... (attempt {Attempt})", i + 1);
            await DelayAsync(2000);
        }

        _logger.LogWarning("Keep sidebar did not load within timeout");
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
            await _page.Mouse.MoveAsync(x, y);
            await Task.Delay(_random.Next(8, 25));
        }

        await _page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Task.Delay(_random.Next(100, 400));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sessionReady = false;

        while (!stoppingToken.IsCancellationRequested && !sessionReady)
        {
            try
            {
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
                await ProcessListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Keep list");

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

    private async Task ProcessListAsync()
    {
        if (_page == null)
            return;

        if (!_isOnKeepPage)
        {
            await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
            _isOnKeepPage = true;
            await HumanDelayAsync(1500, 500);
        }
        else
        {
            await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
            await HumanDelayAsync(1000, 300);
        }

        if (!IsLoggedIn())
        {
            _logger.LogWarning("Session expired. Please log in again in the browser window.");
            _isOnKeepPage = false;
            await EnsureSessionAsync();
            await WaitForKeepLoadedAsync();
            return;
        }

        var listButton = _page.GetByText(_listName, new() { Exact = true }).First;
        if (!await listButton.IsVisibleAsync())
        {
            _logger.LogWarning("List '{ListName}' not found in sidebar", _listName);
            return;
        }

        await MoveMouseToElementAsync(listButton);
        await listButton.ClickAsync();
        await HumanDelayAsync(2000, 500);

        var allCheckboxes = _page.GetByRole(AriaRole.Checkbox);
        var totalCount = await allCheckboxes.CountAsync();

        if (totalCount == 0)
        {
            _logger.LogDebug("No checkboxes found in '{ListName}'", _listName);
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
                    new { Text = text });

                if (response.IsSuccessStatusCode)
                {
                    await MoveMouseToElementAsync(checkbox);
                    await checkbox.ClickAsync();
                    await HumanDelayAsync(500, 200);
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
            _logger.LogDebug("No unchecked items in '{ListName}'", _listName);
        }
        else
        {
            _logger.LogInformation("Processed {Count} unchecked item(s) in '{ListName}'", uncheckedCount, _listName);
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
        _logger.LogInformation("Stopping KeepScraper...");

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
