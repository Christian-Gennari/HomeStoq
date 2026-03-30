namespace HomeStoq.Server.Models;

public class AiCacheEntry
{
    public int Id { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
