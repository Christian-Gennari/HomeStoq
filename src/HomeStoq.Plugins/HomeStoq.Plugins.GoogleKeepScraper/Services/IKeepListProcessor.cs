using Microsoft.Playwright;
using System.Threading.Tasks;

namespace HomeStoq.Plugins.GoogleKeepScraper.Services;

public interface IKeepListProcessor
{
    Task ProcessListsAsync(IPage page, string[] listNames);
}
