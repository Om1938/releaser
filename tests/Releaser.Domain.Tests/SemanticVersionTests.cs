using Releaser.Domain.Common;

namespace Releaser.Domain.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.0", "1.1.0")]
    [InlineData("1.2.0", "1.10.0")]
    [InlineData("1.0.0-beta.1", "1.0.0")]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-rc.1", "1.0.1")]
    public void when_comparing_lower_to_higher_lower_sorts_first(string lower, string higher)
    {
        (SemanticVersion.Parse(lower) < SemanticVersion.Parse(higher)).ShouldBeTrue();
        (SemanticVersion.Parse(higher) > SemanticVersion.Parse(lower)).ShouldBeTrue();
    }

    [Fact]
    public void build_metadata_is_ignored_for_equality()
    {
        SemanticVersion.Parse("1.2.0+build.5").ShouldBe(SemanticVersion.Parse("1.2.0"));
    }

    [Theory]
    [InlineData("v1.2.0")]
    [InlineData("1.2")]
    [InlineData("01.2.0")]
    [InlineData("1.2.0-")]
    [InlineData("")]
    public void when_parsing_invalid_text_parse_fails(string text)
    {
        SemanticVersion.TryParse(text, out _).ShouldBeFalse();
        Should.Throw<DomainRuleException>(() => SemanticVersion.Parse(text));
    }

    [Fact]
    public void to_string_round_trips_the_normalized_version()
    {
        SemanticVersion.Parse("1.2.3-beta.4+meta").ToString().ShouldBe("1.2.3-beta.4");
    }
}
