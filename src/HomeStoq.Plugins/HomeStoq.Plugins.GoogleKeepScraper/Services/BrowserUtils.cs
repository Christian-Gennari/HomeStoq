using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public static class BrowserUtils
{
    private static readonly Random _random = new();

    public static async Task HumanDelayAsync(int baseMs, int jitterMs = 500)
    {
        var delay = baseMs + _random.Next(-jitterMs, jitterMs);
        delay = Math.Max(100, delay);
        await Task.Delay(delay);
    }

    public static async Task DelayAsync(int baseMs)
    {
        var jitter = (int)(baseMs * 0.15);
        var delay = baseMs + _random.Next(-jitter, jitter);
        delay = Math.Max(100, delay);
        await Task.Delay(delay);
    }

    public static async Task MoveMouseToElementAsync(IPage page, ILocator element)
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

    public static async Task TakeScreenshotAsync(IPage page, string filename, string profileDir, ILogger logger)
    {
        try
        {
            var path = Path.Combine(profileDir, filename);
            await page.ScreenshotAsync(new() { Path = path });
            logger.LogInformation("Screenshot saved to {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to take screenshot");
        }
    }
}
