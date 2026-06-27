using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Dtos;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Endpoints;

public static class UrlEndpoints
{
    public static void MapUrlEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/urls").WithTags("Urls");

        group.MapPost("/", CreateShortUrl)
            .WithName("CreateShortUrl")
            .RequireRateLimiting("create-policy")
            .Produces<ShortUrlResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/{code}/analytics", GetAnalytics)
            .WithName("GetAnalytics")
            .Produces<AnalyticsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/{code}", Redirect)
            .WithName("RedirectToOriginal")
            .RequireRateLimiting("redirect-policy")
            .ExcludeFromDescription(); // keeps Swagger clean; root-level catch-all

        app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }))
            .WithName("HealthCheck")
            .ExcludeFromDescription();
    }

    private static async Task<IResult> CreateShortUrl(
        [FromBody] CreateShortUrlRequest request,
        IUrlShortenerService service,
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(
            request.OriginalUrl, request.CustomAlias, request.ExpiresInDays, request.IdempotencyKey, ct);

        switch (result.Status)
        {
            case CreateUrlResultStatus.ValidationError:
                return Results.BadRequest(new ErrorResponse("validation_error", result.Error));

            case CreateUrlResultStatus.AliasTaken:
                return Results.Conflict(new ErrorResponse("alias_taken", result.Error));

            case CreateUrlResultStatus.Created:
            case CreateUrlResultStatus.IdempotentReplay:
                var entity = result.ShortUrl!;
                var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
                var response = new ShortUrlResponse(
                    entity.ShortCode,
                    $"{baseUrl}/{entity.ShortCode}",
                    entity.OriginalUrl,
                    entity.CreatedAtUtc,
                    entity.ExpiresAtUtc);

                return result.Status == CreateUrlResultStatus.Created
                    ? Results.Created($"/api/urls/{entity.ShortCode}", response)
                    : Results.Ok(response); // replay: same resource, not newly created

            default:
                return Results.Problem("Unexpected result status.");
        }
    }

    private static async Task<IResult> Redirect(
        string code,
        IUrlShortenerService service,
        HttpRequest httpRequest,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var result = await service.ResolveAsync(code, ct);

        if (result.Status is ResolveResultStatus.NotFound)
        {
            return Results.NotFound(new ErrorResponse("not_found", $"No URL found for code '{code}'."));
        }
        if (result.Status is ResolveResultStatus.Expired)
        {
            return Results.Json(new ErrorResponse("expired", "This short URL has expired."), statusCode: StatusCodes.Status410Gone);
        }
        if (result.Status is ResolveResultStatus.Inactive)
        {
            return Results.Json(new ErrorResponse("inactive", "This short URL has been deactivated."), statusCode: StatusCodes.Status410Gone);
        }

        var entity = result.ShortUrl!;

        // Analytics recording must never block or break the redirect — best-effort only.
        try
        {
            var referrer = httpRequest.Headers.Referer.ToString();
            var userAgent = httpRequest.Headers.UserAgent.ToString();
            var ipHash = HashIp(httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString());
            await service.RecordClickAsync(entity.Id, referrer, userAgent, ipHash, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record click for {Code}; redirect still proceeds.", code);
        }

        return Results.Redirect(entity.OriginalUrl, permanent: false);
    }

    private static async Task<IResult> GetAnalytics(string code, IUrlShortenerService service, CancellationToken ct)
    {
        var analytics = await service.GetAnalyticsAsync(code, ct);
        return analytics is null
            ? Results.NotFound(new ErrorResponse("not_found", $"No URL found for code '{code}'."))
            : Results.Ok(analytics);
    }

    /// <summary>SHA-256 hash of the client IP. We retain a hash (not the raw IP) for
    /// rough abuse-detection/uniqueness signals without storing PII. See ARCHITECTURE.md "Privacy".</summary>
    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes);
    }
}
