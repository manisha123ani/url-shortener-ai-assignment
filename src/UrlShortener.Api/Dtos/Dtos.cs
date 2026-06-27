namespace UrlShortener.Api.Dtos;

public record CreateShortUrlRequest(
    string OriginalUrl,
    string? CustomAlias = null,
    int? ExpiresInDays = null,
    string? IdempotencyKey = null
);

public record ShortUrlResponse(
    string ShortCode,
    string ShortUrl,
    string OriginalUrl,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc
);

public record AnalyticsResponse(
    string ShortCode,
    string OriginalUrl,
    long TotalClicks,
    DateTime? LastClickedAtUtc,
    IReadOnlyList<DailyClickCount> ClicksByDay,
    IReadOnlyList<ReferrerCount> TopReferrers
);

public record DailyClickCount(DateOnly Date, long Count);

public record ReferrerCount(string Referrer, long Count);

public record ErrorResponse(string Error, string? Detail = null);
