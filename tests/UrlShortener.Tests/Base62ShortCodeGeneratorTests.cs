using FluentAssertions;
using UrlShortener.Api.Services;
using Xunit;

namespace UrlShortener.Tests;

public class Base62ShortCodeGeneratorTests
{
    private readonly Base62ShortCodeGenerator _sut = new();

    [Theory]
    [InlineData(1, "1")]
    [InlineData(0, null)] // expect throw, see test below
    [InlineData(61, "Z")]
    [InlineData(62, "10")]
    public void FromId_EncodesKnownValuesCorrectly(long id, string? expected)
    {
        if (expected is null) return; // handled by the throw test
        _sut.FromId(id).Should().Be(expected);
    }

    [Fact]
    public void FromId_ZeroOrNegative_Throws()
    {
        Action act = () => _sut.FromId(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromId_IsDeterministic()
    {
        _sut.FromId(12345).Should().Be(_sut.FromId(12345));
    }

    [Fact]
    public void FromId_DifferentIdsProduceDifferentCodes()
    {
        _sut.FromId(100).Should().NotBe(_sut.FromId(101));
    }

    [Fact]
    public void RandomCode_ProducesRequestedLength()
    {
        _sut.RandomCode(10).Length.Should().Be(10);
    }

    [Fact]
    public void RandomCode_OnlyContainsAlphanumericChars()
    {
        var code = _sut.RandomCode(50);
        code.Should().MatchRegex("^[0-9a-zA-Z]+$");
    }
}
