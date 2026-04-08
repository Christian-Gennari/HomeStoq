using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public class CdpBrowserService : IBrowserService, IDisposable
{
    private readonly ILogger<CdpBrowserService> _logger;
    private readonly string _profileDir;
    private readonly int _maxRelaunchAttempts;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private Process? _chromeProcess;
    private string _cdpUrl = "http://localhost:9222";
    private int _relaunchAttempt = 0;

    private readonly string? _username;
    private readonly string? _password;

    public bool IsOnKeepPage { get; set; }
    public bool IsConnected => _browser?.IsConnected == true;

    public CdpBrowserService(ILogger<CdpBrowserService> logger, IConfiguration config)
    {
        _logger = logger;
        _profileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeStoq", "chrome-profile");
        _maxRelaunchAttempts = int.Parse(config["GoogleKeepScraper:ChromeRelaunchAttempts"] ?? "5");
        _username = Environment.GetEnvironmentVariable("GOOGLE_USERNAME");
        _password = Environment.GetEnvironmentVariable("GOOGLE_PASSWORD");
    }

    public async Task InitBrowserAsync()
    {
        _playwright = await Playwright.CreateAsync();
        await LaunchAndConnectAsync();
    }

    private async Task LaunchAndConnectAsync()
    {
        // 1. Try to connect to existing Chrome (in case user launched manually)
        if (await TryConnectAsync())
        {
            _logger.LogInformation("Connected to existing Chrome instance");
            return;
        }

        // 2. Launch Chrome ourselves
        await LaunchChromeAsync();
        await ConnectWithRetryAsync();
    }

    private async Task LaunchChromeAsync()
    {
        var chromePath = ChromeLocator.FindChrome()
            ?? throw new InvalidOperationException(
                "Google Chrome not found. Please install Google Chrome from https://www.google.com/chrome/");

        var port = ChromeLocator.FindAvailablePort(9222);
        Directory.CreateDirectory(_profileDir);

        var args = new[]
        {
            $"--remote-debugging-port={port}",
            $"--user-data-dir={_profileDir}",
            "--no-sandbox",  // Required when running Chrome as root in Docker
            "--disable-setuid-sandbox",  // Additional sandbox disable for containers
            "--disable-gpu",  // Disable GPU acceleration (fixes WebGL blocklist errors)
            "--disable-software-rasterizer",  // Disable software rendering fallback
            "--disable-dev-shm-usage",  // Use /tmp instead of /dev/shm in containers
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-infobars",
            "--window-size=1280,720",
            "--disable-features=VizDisplayCompositor,SiteIsolationForPasswordSites",  // Additional stability flags
            "https://keep.google.com"
        };

        var psi = new ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = string.Join(" ", args),
            UseShellExecute = true,
        };

        _chromeProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Chrome");

        _cdpUrl = $"http://localhost:{port}";
        _logger.LogInformation("Launched Chrome with CDP on port {Port}", port);

        // Wait for Chrome to start listening
        await Task.Delay(3000);
    }

    private async Task<bool> TryConnectAsync()
    {
        try
        {
            _browser = await _playwright!.Chromium.ConnectOverCDPAsync(_cdpUrl);
            _page = await FindOrCreateKeepPageAsync();
            _relaunchAttempt = 0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ConnectWithRetryAsync()
    {
        while (_relaunchAttempt < _maxRelaunchAttempts)
        {
            try
            {
                _browser = await _playwright!.Chromium.ConnectOverCDPAsync(_cdpUrl);
                _page = await FindOrCreateKeepPageAsync();
                _relaunchAttempt = 0;
                _logger.LogInformation("Connected to Chrome via CDP at {Url}", _cdpUrl);
                return;
            }
            catch (Exception ex)
            {
                _relaunchAttempt++;
                var delayMs = Math.Min(10000 * _relaunchAttempt, 300000); // Cap at 5 minutes

                _logger.LogWarning(ex,
                    "Failed to connect to Chrome (attempt {Attempt}/{Max}). Retrying in {Delay}s...",
                    _relaunchAttempt, _maxRelaunchAttempts, delayMs / 1000);

                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to Chrome after {_maxRelaunchAttempts} attempts. " +
            "Ensure Chrome is installed or try switching to Playwright mode in config.ini: BrowserMode=Playwright");
    }

    private async Task<IPage> FindOrCreateKeepPageAsync()
    {
        if (_browser == null)
            throw new InvalidOperationException("Browser not initialized");

        var contexts = _browser.Contexts;

        // Search for existing Keep tab
        foreach (var context in contexts)
        {
            foreach (var page in context.Pages)
            {
                if (page.Url.Contains("keep.google.com"))
                {
                    _logger.LogDebug("Found existing Keep tab");
                    return page;
                }
            }
        }

        // Create new page
        _logger.LogDebug("Creating new page for Google Keep");
        return await contexts[0].NewPageAsync();
    }

    public async Task<bool> EnsureSessionAsync()
    {
        if (_page == null)
            return false;

        await _page.GotoAsync("https://keep.google.com");
        IsOnKeepPage = true;

        if (IsLoggedIn())
        {
            _logger.LogInformation("Existing session found on Keep");
            return true;
        }

        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            try
            {
                _logger.LogInformation("Attempting automatic login for {User}...", _username);

                // Handle Username
                var emailInput = _page.Locator("input[type='email']");
                if (await emailInput.IsVisibleAsync())
                {
                    await BrowserUtils.HumanDelayAsync(1000, 500);
                    await BrowserUtils.TypeSlowlyAsync(_page, _username);
                    await BrowserUtils.HumanDelayAsync(500, 200);
                    await _page.Keyboard.PressAsync("Enter");
                    await BrowserUtils.HumanDelayAsync(2000, 500);
                }

                // Handle Password
                var passwordInput = _page.Locator("input[type='password']");
                // Wait up to 5s for password field
                for (int i = 0; i < 5 && !await passwordInput.IsVisibleAsync(); i++)
                    await Task.Delay(1000);

                if (await passwordInput.IsVisibleAsync())
                {
                    await BrowserUtils.HumanDelayAsync(1000, 500);
                    await BrowserUtils.TypeSlowlyAsync(_page, _password);
                    await BrowserUtils.HumanDelayAsync(500, 200);
                    await _page.Keyboard.PressAsync("Enter");
                    await BrowserUtils.HumanDelayAsync(3000, 1000);
                }

                if (IsLoggedIn())
                {
                    _logger.LogInformation("Automatic login successful!");
                    return true;
                }
                
                _logger.LogWarning("Automatic login did not complete (2FA or CAPTCHA might be required)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic login attempt");
            }
        }

        _logger.LogInformation("No saved session found.");
        _logger.LogInformation("Please log in to Google Keep in the Chrome window.");
        _logger.LogInformation("If running in Docker, use the noVNC interface at http://localhost:6080 to log in.");
        _logger.LogInformation("The scraper will detect your session and start monitoring automatically.");

        for (var i = 0; i < 600; i++)
        {
            await BrowserUtils.DelayAsync(3000);
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

            try
            {
                if (await sidebar.IsVisibleAsync() || await checkboxes.IsVisibleAsync())
                    return;
            }
            catch { /* Element might not exist yet */ }

            _logger.LogDebug("Waiting for Keep to load... (attempt {Attempt})", i + 1);
            await BrowserUtils.DelayAsync(2000);
        }

        _logger.LogWarning("Keep did not load within timeout");
        await BrowserUtils.TakeScreenshotAsync(_page, "keep-not-loaded.png", _profileDir, _logger);
    }

    public bool IsLoggedIn()
    {
        var url = _page?.Url ?? "";
        return !url.Contains("accounts.google.com")
            && !url.Contains("ServiceLogin")
            && !string.IsNullOrEmpty(url);
    }

    public Task<IPage?> GetPageAsync()
    {
        return Task.FromResult(_page);
    }

    public Task PerformRandomActivityAsync()
    {
        // In CDP mode, skip random activity (real user behavior)
        return Task.CompletedTask;
    }

    public async Task RecoverAsync()
    {
        if (_chromeProcess?.HasExited == false)
        {
            // Chrome running but connection lost - try reconnect
            try
            {
                if (await TryConnectAsync())
                {
                    _logger.LogInformation("Recovery: Reconnected to existing Chrome");
                    return;
                }
            }
            catch { /* Connection failed */ }
        }

        // Chrome is dead - relaunch
        _logger.LogWarning("Chrome process not responding. Relaunching...");
        _relaunchAttempt = 0;
        CleanupChromeProcess();
        await LaunchChromeAsync();
        await ConnectWithRetryAsync();
    }

    public async Task CloseBrowserAsync()
    {
        _logger.LogInformation("Closing Chrome...");

        try
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing browser connection");
        }

        CleanupChromeProcess();
        _playwright?.Dispose();
    }

    private void CleanupChromeProcess()
    {
        try
        {
            if (_chromeProcess != null && !_chromeProcess.HasExited)
            {
                _logger.LogDebug("Terminating Chrome process (PID: {PID})", _chromeProcess.Id);
                _chromeProcess.Kill(entireProcessTree: true);
                _chromeProcess.WaitForExit(5000);
                _logger.LogDebug("Chrome process terminated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating Chrome process");
        }
        finally
        {
            _chromeProcess?.Dispose();
            _chromeProcess = null;
        }
    }

    public void Dispose()
    {
        CloseBrowserAsync().GetAwaiter().GetResult();
    }
}
