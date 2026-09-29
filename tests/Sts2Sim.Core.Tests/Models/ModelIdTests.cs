namespace Sts2Sim.Core.Tests.Models;

using System.Text.Json;
using Sts2Sim.Core.Models;

public class ModelIdTests
{
    [Fact]
    public void Constructor_RejectsCategoryEndingInModel()
    {
        Assert.Throws<ArgumentException>(() => new ModelId("CHARACTER_MODEL", "REGENT"));
    }

    [Fact]
    public void ToString_IsCategoryDotEntry()
    {
        Assert.Equal("CHARACTER.REGENT", new ModelId("CHARACTER", "REGENT").ToString());
    }

    [Fact]
    public void Deserialize_RoundTrips()
    {
        var id = ModelId.Deserialize("CHARACTER.REGENT");
        Assert.Equal(new ModelId("CHARACTER", "REGENT"), id);
    }

    [Theory]
    [InlineData("NO_DOT")]
    [InlineData("TOO.MANY.DOTS")]
    public void Deserialize_InvalidForm_ThrowsJsonException(string input)
    {
        Assert.Throws<JsonException>(() => ModelId.Deserialize(input));
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(new ModelId("CARD", "STRIKE"), new ModelId("CARD", "STRIKE"));
        Assert.NotEqual(new ModelId("CARD", "STRIKE"), new ModelId("RELIC", "STRIKE"));
    }

    [Fact]
    public void CompareTo_OrdersByCategoryThenEntry()
    {
        var a = new ModelId("CARD", "BASH");
        var b = new ModelId("CARD", "STRIKE");
        var c = new ModelId("RELIC", "ANCHOR");
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(c) < 0);
        Assert.True(c.CompareTo(null) > 0);
        Assert.Equal(0, a.CompareTo(new ModelId("CARD", "BASH")));
    }

    [Fact]
    public void SlugifyCategory_StripsModelSuffix()
    {
        Assert.Equal("CHARACTER", ModelId.SlugifyCategory("CharacterModel"));
        Assert.Equal("CARD", ModelId.SlugifyCategory("CardModel"));
        // 不以 Model 结尾的类别名保持原样 slugify
        Assert.Equal("GOLDEN_AMULET", ModelId.SlugifyCategory("GoldenAmulet"));
    }
}
