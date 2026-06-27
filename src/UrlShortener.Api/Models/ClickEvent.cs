namespace UrlShortener.Api.Models;

/// <summary>
/// One row per redirect/click, used to compute analytics (counts, referrers, time buckets).
/// Kept intentionally lightweight; for high-volume production use this would be
/// written to a queue (e.g. Azure Service Bus / Event Hub) and aggregated asynchronously
/// instead of synchronously on the redirect hot path. See ARCHITECTURE.md, "Scalability".
/// </summary>
public class ClickEvent
{
    public long Id { get; set; }

    public long ShortUrlId { get; set; }

    public ShortUrl? ShortUrl { get; set; }

    public DateTime ClickedAtUtc { get; set; }

    public string? Referrer { get; set; }

    public string? UserAgent { get; set; }

    public string? IpHash { get; set; } // hashed, never raw IP — see ARCHITECTURE.md "Privacy"
}
