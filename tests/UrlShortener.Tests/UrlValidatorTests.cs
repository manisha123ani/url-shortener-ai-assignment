using FluentAssertions;
using UrlShortener.Api.Services;
using Xunit;

namespace UrlShortener.Tests;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/path?query=1")]
    [InlineData("https://sub.example.co.in/a/b/c")]
    public void TryValidate_AcceptsWellFormedHttpUrls(string url)
    {
        UrlValidator.TryValidate(url, out var uri, out var error).Should().BeTrue();
        uri.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("http://localhost:5000/admin")]
    [InlineData("http://127.0.0.1/internal")]
    public void TryValidate_RejectsInvalidOrUnsafeUrls(string? url)
    {
        UrlValidator.TryValidate(url, out var uri, out var error).Should().BeFalse();
        uri.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryValidate_RejectsExcessivelyLongUrls()
    {
        var longUrl = "https://example.com/" + new string('a', 3000);
        UrlValidator.TryValidate(longUrl, out _, out var error).Should().BeFalse();
        error.Should().Contain("exceeds maximum length");
    }

    [Theory]
    [InlineData("ab")] // too short
    [InlineData("this-alias-is-way-too-long-to-allow")] // too long
    [InlineData("has space")]
    [InlineData("has/slash")]
    public void IsValidAlias_RejectsInvalidAliases(string alias)
    {
        UrlValidator.IsValidAlias(alias).Should().BeFalse();
    }

    [Theory]
    [InlineData("my-alias")]
    [InlineData("My_Alias2")]
    [InlineData("abc")]
    public void IsValidAlias_AcceptsValidAliases(string alias)
    {
        UrlValidator.IsValidAlias(alias).Should().BeTrue();
    }
}
