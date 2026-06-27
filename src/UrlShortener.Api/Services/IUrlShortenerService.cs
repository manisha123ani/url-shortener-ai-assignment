using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services;

public enum CreateUrlResultStatus { Created, AliasTaken, ValidationError, IdempotentReplay }

public record CreateUrlResult(CreateUrlResultStatus Status, ShortUrl? ShortUrl, string? Error);

public enum ResolveResultStatus { Found, NotFound, Expired, Inactive }

public record ResolveResult(ResolveResultStatus Status, ShortUrl? ShortUrl);

public interface IUrlShortenerService
{
    Task<CreateUrlResult> CreateAsync(
        string originalUrl,
        string? customAlias,
        int? expiresInDays,
        string? idempotencyKey,
        CancellationToken ct);

    Task<ResolveResult> ResolveAsync(string shortCode, CancellationToken ct);

    Task RecordClickAsync(long shortUrlId, string? referrer, string? userAgent, string? ipHash, CancellationToken ct);

    Task<Dtos.AnalyticsResponse?> GetAnalyticsAsync(string shortCode, CancellationToken ct);
}
