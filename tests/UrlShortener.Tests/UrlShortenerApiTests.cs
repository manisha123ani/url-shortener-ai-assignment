using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Dtos;
using Xunit;

namespace UrlShortener.Tests;

/// <summary>
/// Spins up the real app pipeline (DI, middleware, rate limiting, Swagger, etc.)
/// against an isolated SQLite file per test run, hitting actual HTTP endpoints.
/// This is the "does it actually work end-to-end" layer referenced in the
/// Final Engineering Summary's testing approach.
/// </summary>
public class UrlShortenerApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UrlShortenerApiTests(WebApplicationFactory<Program> factory)
    {
        var dbFile = $"test_{Guid.NewGuid():N}.db";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={dbFile}");
        });
    }

    [Fact]
    public async Task CreateThenRedirect_FullFlow_Succeeds()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var createResponse = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest("https://example.com/integration-test"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ShortUrlResponse>();
        created.Should().NotBeNull();

        var redirectResponse = await client.GetAsync($"/{created!.ShortCode}");
        redirectResponse.StatusCode.Should().Be(HttpStatusCode.Found);
        redirectResponse.Headers.Location!.ToString().Should().Be("https://example.com/integration-test");
    }

    [Fact]
    public async Task Create_InvalidUrl_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/urls", new CreateShortUrlRequest("not-a-url"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Redirect_UnknownCode_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/totallyUnknownCode");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analytics_AfterClicks_ReflectsCorrectCount()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var createResponse = await client.PostAsJsonAsync("/api/urls",
            new CreateShortUrlRequest("https://example.com/analytics-test", CustomAlias: "anatest"));
        createResponse.EnsureSuccessStatusCode();

        await client.GetAsync("/anatest");
        await client.GetAsync("/anatest");
        await client.GetAsync("/anatest");

        var analyticsResponse = await client.GetAsync("/api/urls/anatest/analytics");
        analyticsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<AnalyticsResponse>();
        analytics!.TotalClicks.Should().Be(3);
    }

    [Fact]
    public async Task Create_DuplicateAlias_Returns409()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/urls", new CreateShortUrlRequest("https://example.com/1", CustomAlias: "conflictalias"));
        var second = await client.PostAsJsonAsync("/api/urls", new CreateShortUrlRequest("https://example.com/2", CustomAlias: "conflictalias"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
