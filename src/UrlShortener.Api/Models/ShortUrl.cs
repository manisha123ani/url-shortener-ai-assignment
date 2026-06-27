namespace UrlShortener.Api.Models;

/// <summary>
/// Represents a shortened URL mapping. ShortCode is the human-facing key (base62),
/// while Id (auto-increment) is used internally as the seed for code generation.
/// </summary>
public class ShortUrl
{
    public long Id { get; set; }

    public string ShortCode { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Optional caller-supplied idempotency key to avoid duplicate creation on retries.</summary>
    public string? IdempotencyKey { get; set; }

    public bool IsActive { get; set; } = true;

    public long ClickCount { get; set; }

    public ICollection<ClickEvent> ClickEvents { get; set; } = new List<ClickEvent>();
}
