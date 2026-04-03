using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public class PlaywrightBrowserService : IBrowserService
{
    private readonly ILogger<PlaywrightBrowserService> _logger;
    private readonly Random _random = Random.Shared;

    private IPlaywright? _playwright;
    private IBrowserContext? _browserContext;
    private IPage? _page;

    private readonly string _profileDir;
    public bool IsOnKeepPage { get; set; }

    public PlaywrightBrowserService(ILogger<PlaywrightBrowserService> logger)
    {
        _logger = logger;
        _profileDir = Path.GetFullPath("browser-profile");
    }

    public async Task InitBrowserAsync()
    {
        _playwright = await Playwright.CreateAsync();
        await EnsureBrowserContextAsync();
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

    public Task<IPage?> GetPageAsync()
    {
        return Task.FromResult(_page);
    }

    public bool IsLoggedIn()
    {
        var url = _page?.Url ?? "";
        return !url.Contains("accounts.google.com")
            && !url.Contains("ServiceLogin")
            && !string.IsNullOrEmpty(url);
    }

    public async Task<bool> EnsureSessionAsync()
    {
        if (_page == null)
            return false;

        await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
        IsOnKeepPage = true;

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

    public async Task WaitForKeepLoadedAsync()
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

    public async Task PerformRandomActivityAsync()
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

    public async Task RecoverAsync()
    {
        _logger.LogInformation("Attempting recovery...");

        try
        {
            if (_page != null)
            {
                await _page.GotoAsync("https://keep.google.com", new() { WaitUntil = WaitUntilState.Load });
                IsOnKeepPage = true;
                await HumanDelayAsync(2000, 500);

                if (IsLoggedIn())
                {
                    _logger.LogInformation("Recovery successful, session still valid");
                    return;
                }
            }

            _logger.LogInformation("Session expired. Please log in again in the browser window.");
            IsOnKeepPage = false;
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

    public async Task CloseBrowserAsync()
    {
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
}
