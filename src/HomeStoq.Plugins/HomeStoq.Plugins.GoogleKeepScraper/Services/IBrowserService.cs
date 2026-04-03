using Microsoft.Playwright;
using System.Threading.Tasks;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public interface IBrowserService
{
    Task InitBrowserAsync();
    Task CloseBrowserAsync();
    Task<IPage?> GetPageAsync();
    Task<bool> EnsureSessionAsync();
    Task WaitForKeepLoadedAsync();
    Task PerformRandomActivityAsync();
    Task RecoverAsync();
    bool IsOnKeepPage { get; set; }
    bool IsLoggedIn();
}
