using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using HomeStoq.Plugins.GoogleKeepScraper.Services;

namespace HomeStoq.Plugins.GoogleKeepScraper;

public class GoogleKeepScraperWorker : BackgroundService
{
    private readonly ILogger<GoogleKeepScraperWorker> _logger;
    private readonly IBrowserService _browserService;
    private readonly IKeepListProcessor _keepListProcessor;
    private readonly Random _random = Random.Shared;

    private readonly string[] _listNames;
    private readonly int _pollIntervalSeconds;
    private readonly int _pollIntervalJitterSeconds;
    private readonly int _activeHoursStart;
    private readonly int _activeHoursEnd;

    public GoogleKeepScraperWorker(
        IBrowserService browserService,
        IKeepListProcessor keepListProcessor,
        IConfiguration config,
        ILogger<GoogleKeepScraperWorker> logger)
    {
        _browserService = browserService;
        _keepListProcessor = keepListProcessor;
        _logger = logger;

        var listNamesConfig = config["GoogleKeepScraper:KeepListName"] ?? "inköpslistan";
        _listNames = listNamesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        _pollIntervalSeconds = int.Parse(config["GoogleKeepScraper:PollIntervalSeconds"] ?? "45");
        _pollIntervalJitterSeconds = int.Parse(config["GoogleKeepScraper:PollIntervalJitterSeconds"] ?? "15");

        var activeHours = config["GoogleKeepScraper:ActiveHours"] ?? "07-23";
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

        await _browserService.InitBrowserAsync();

        _logger.LogInformation("GoogleKeepScraper initialized. Monitoring lists: {ListNames}", string.Join(", ", _listNames));

        await base.StartAsync(cancellationToken);
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

                sessionReady = await _browserService.EnsureSessionAsync();
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

        await _browserService.WaitForKeepLoadedAsync();

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

                await _browserService.PerformRandomActivityAsync();
                await ProcessListsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Keep lists");

                try
                {
                    await _browserService.RecoverAsync();
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
        var page = await _browserService.GetPageAsync();
        if (page == null)
            return;

        if (!_browserService.IsOnKeepPage || !_browserService.IsLoggedIn())
        {
            await page.GotoAsync("https://keep.google.com", new() { WaitUntil = Microsoft.Playwright.WaitUntilState.Load });
            _browserService.IsOnKeepPage = true;
            await HumanDelayAsync(1500, 500);

            if (!_browserService.IsLoggedIn())
            {
                _logger.LogWarning("Session expired. Please log in again in the browser window.");
                _browserService.IsOnKeepPage = false;
                await _browserService.EnsureSessionAsync();
                await _browserService.WaitForKeepLoadedAsync();
                return;
            }
        }
        else
        {
            await page.ReloadAsync(new() { WaitUntil = Microsoft.Playwright.WaitUntilState.Load });
            await HumanDelayAsync(1000, 300);
        }

        await _keepListProcessor.ProcessListsAsync(page, _listNames);
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
            return now >= _activeHoursStart || now < _activeHoursEnd;
        }
    }

    private async Task PollDelayAsync(CancellationToken token)
    {
        var delay = (_pollIntervalSeconds + _random.Next(-_pollIntervalJitterSeconds, _pollIntervalJitterSeconds)) * 1000;
        delay = Math.Max(10000, delay);
        await Task.Delay(delay, token);
    }

    private async Task HumanDelayAsync(int baseMs, int jitterMs = 500)
    {
        var delay = baseMs + _random.Next(-jitterMs, jitterMs);
        delay = Math.Max(100, delay);
        await Task.Delay(delay);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping GoogleKeepScraper and closing browser...");

        await _browserService.CloseBrowserAsync();

        await base.StopAsync(cancellationToken);
    }
}
