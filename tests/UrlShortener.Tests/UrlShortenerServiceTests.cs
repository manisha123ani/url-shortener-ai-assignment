using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UrlShortener.Api.Data;
using UrlShortener.Api.Services;
using Xunit;

namespace UrlShortener.Tests;

/// <summary>
/// Uses a real SQLite in-memory database (not EF's InMemory provider) so that
/// constraints, transactions, and unique indexes behave exactly as in production —
/// EF's InMemory provider silently ignores unique-index violations, which would mask bugs.
/// </summary>
public class UrlShortenerServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly UrlShortenerService _sut;

    public UrlShortenerServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _sut = new UrlShortenerService(
            _db,
            new Base62ShortCodeGenerator(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<UrlShortenerService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ValidUrl_CreatesAndAssignsBase62Code()
    {
        var result = await _sut.CreateAsync("https://example.com/foo", null, null, null, CancellationToken.None);

        result.Status.Should().Be(CreateUrlResultStatus.Created);
        result.ShortUrl!.ShortCode.Should().NotBeNullOrEmpty();
        result.ShortUrl.ShortCode.Should().MatchRegex("^[0-9a-zA-Z]+$");
    }

    [Fact]
    public async Task CreateAsync_InvalidUrl_ReturnsValidationError()
    {
        var result = await _sut.CreateAsync("not-a-url", null, null, null, CancellationToken.None);
        result.Status.Should().Be(CreateUrlResultStatus.ValidationError);
    }

    [Fact]
    public async Task CreateAsync_CustomAlias_UsesAliasAsShortCode()
    {
        var result = await _sut.CreateAsync("https://example.com", "myalias", null, null, CancellationToken.None);
        result.Status.Should().Be(CreateUrlResultStatus.Created);
        result.ShortUrl!.ShortCode.Should().Be("myalias");
    }

    [Fact]
    public async Task CreateAsync_DuplicateAlias_ReturnsAliasTaken()
    {
        await _sut.CreateAsync("https://example.com/a", "dupe", null, null, CancellationToken.None);
        var second = await _sut.CreateAsync("https://example.com/b", "dupe", null, null, CancellationToken.None);

        second.Status.Should().Be(CreateUrlResultStatus.AliasTaken);
    }

    [Fact]
    public async Task CreateAsync_SameIdempotencyKeyTwice_ReturnsReplayNotDuplicate()
    {
        var first = await _sut.CreateAsync("https://example.com/x", null, null, "key-1", CancellationToken.None);
        var second = await _sut.CreateAsync("https://example.com/x", null, null, "key-1", CancellationToken.None);

        first.Status.Should().Be(CreateUrlResultStatus.Created);
        second.Status.Should().Be(CreateUrlResultStatus.IdempotentReplay);
        second.ShortUrl!.Id.Should().Be(first.ShortUrl!.Id);
    }

    [Fact]
    public async Task ResolveAsync_UnknownCode_ReturnsNotFound()
    {
        var result = await _sut.ResolveAsync("doesnotexist", CancellationToken.None);
        result.Status.Should().Be(ResolveResultStatus.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_ExpiredUrl_ReturnsExpired()
    {
        var created = await _sut.CreateAsync("https://example.com/expiring", "expalias", -1, null, CancellationToken.None);
        var result = await _sut.ResolveAsync(created.ShortUrl!.ShortCode, CancellationToken.None);

        result.Status.Should().Be(ResolveResultStatus.Expired);
    }

    [Fact]
    public async Task ResolveAsync_ActiveUrl_ReturnsFound()
    {
        var created = await _sut.CreateAsync("https://example.com/active", "actalias", null, null, CancellationToken.None);
        var result = await _sut.ResolveAsync(created.ShortUrl!.ShortCode, CancellationToken.None);

        result.Status.Should().Be(ResolveResultStatus.Found);
        result.ShortUrl!.OriginalUrl.Should().Be("https://example.com/active");
    }

    [Fact]
    public async Task RecordClickAsync_IncrementsClickCountAndCreatesEvent()
    {
        var created = await _sut.CreateAsync("https://example.com/click", "clickalias", null, null, CancellationToken.None);

        await _sut.RecordClickAsync(created.ShortUrl!.Id, "https://referrer.com", "test-agent", "hash123", CancellationToken.None);
        await _sut.RecordClickAsync(created.ShortUrl!.Id, null, null, null, CancellationToken.None);

        var analytics = await _sut.GetAnalyticsAsync("clickalias", CancellationToken.None);

        analytics.Should().NotBeNull();
        analytics!.TotalClicks.Should().Be(2);
        analytics.TopReferrers.Should().Contain(r => r.Referrer == "https://referrer.com");
        analytics.TopReferrers.Should().Contain(r => r.Referrer == "(direct)");
    }

    [Fact]
    public async Task GetAnalyticsAsync_UnknownCode_ReturnsNull()
    {
        var analytics = await _sut.GetAnalyticsAsync("nope", CancellationToken.None);
        analytics.Should().BeNull();
    }
}
