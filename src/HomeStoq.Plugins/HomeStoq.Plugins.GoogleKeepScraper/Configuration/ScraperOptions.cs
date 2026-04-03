namespace HomeStoq.Plugins.GoogleKeepScraper.Configuration;

public class ScraperOptions
{
    public int PollIntervalSeconds { get; set; } = 45;
    public int PollIntervalJitterSeconds { get; set; } = 15;
    public string ActiveHours { get; set; } = "07-23";
}
