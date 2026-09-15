using TestShieldAI.Engine;

namespace TestShieldAI.Engine.Tests;

public class OpenApiSpecKeyTests
{
    [Fact]
    public void FromTitle_UsesTrimmedInfoTitle()
    {
        Assert.Equal("Pets", OpenApiSpecKey.FromTitle("Pets"));
        Assert.Equal("Customer API", OpenApiSpecKey.FromTitle("  Customer API  "));
    }

    [Fact]
    public void FromTitle_CollapsesRepeatedWhitespace()
    {
        Assert.Equal("Customer API", OpenApiSpecKey.FromTitle("Customer   \t API"));
        Assert.Equal("Order Service", OpenApiSpecKey.CanonicalDisplay("  Order   Service  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromTitle_BlankTitle_UsesDeterministicFallback(string? title)
    {
        Assert.Equal(OpenApiSpecKey.UntitledFallback, OpenApiSpecKey.FromTitle(title));
        Assert.Equal("", OpenApiSpecKey.CanonicalDisplay(title));
        Assert.Equal("", OpenApiSpecKey.ComparisonKey(title));
    }

    [Fact]
    public void ComparisonKey_IsCaseInsensitive()
    {
        Assert.Equal(
            OpenApiSpecKey.ComparisonKey("Customer API"),
            OpenApiSpecKey.ComparisonKey("customer api"));
        Assert.NotEqual(
            OpenApiSpecKey.ComparisonKey("Customer"),
            OpenApiSpecKey.ComparisonKey("Order"));
    }

    [Fact]
    public void ComparisonKey_MissingSpecKey_MatchesGate1EmptyIdentity()
    {
        Assert.Equal("", OpenApiSpecKey.ComparisonKey(null));
        Assert.Equal("", OpenApiSpecKey.ComparisonKey(""));
        Assert.Equal("", OpenApiSpecKey.ComparisonKey("  "));
    }

    [Fact]
    public void FromTitle_DifferentTitles_ProduceDifferentKeys()
    {
        Assert.NotEqual(OpenApiSpecKey.FromTitle("Customer"), OpenApiSpecKey.FromTitle("Order"));
        Assert.NotEqual(
            OpenApiSpecKey.ComparisonKey(OpenApiSpecKey.FromTitle("Customer")),
            OpenApiSpecKey.ComparisonKey(OpenApiSpecKey.FromTitle("Order")));
    }

    [Fact]
    public void FromTitle_SameTitle_IsTheSameIntegrationIdentity()
    {
        Assert.Equal(OpenApiSpecKey.FromTitle("Pets"), OpenApiSpecKey.FromTitle("Pets"));
        Assert.Equal(
            OpenApiSpecKey.ComparisonKey("Pets"),
            OpenApiSpecKey.ComparisonKey("pets"));
    }

    [Fact]
    public void FromTitle_IgnoresOpenApiVersion()
    {
        var first = OpenApiSpecKey.FromTitle("Pets");
        var second = OpenApiSpecKey.FromTitle("Pets");

        Assert.Equal(first, second);
        Assert.Equal("Pets", first);
    }
}
