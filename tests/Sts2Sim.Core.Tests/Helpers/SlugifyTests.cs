namespace Sts2Sim.Core.Tests.Helpers;

using Sts2Sim.Core.Helpers;

public class SlugifyTests
{
    // 黄金值:按游戏 StringHelper.Slugify 算法(CamelCase 加下划线 → 大写 → 空白转 _ → 剔除非 [A-Z0-9_])
    [Theory]
    [InlineData("Regent", "REGENT")]
    [InlineData("StrikeRegent", "STRIKE_REGENT")]
    [InlineData("FallingStar", "FALLING_STAR")]
    [InlineData("CharacterModel", "CHARACTER_MODEL")]
    [InlineData("CardModel", "CARD_MODEL")]
    [InlineData("AscendersBane", "ASCENDERS_BANE")]
    [InlineData("  Trimmed  ", "TRIMMED")]
    [InlineData("With Space", "WITH_SPACE")]
    [InlineData("Dash-Bang!", "DASHBANG")]
    public void Slugify_MatchesGameAlgorithm(string input, string expected)
    {
        Assert.Equal(expected, StringHelper.Slugify(input));
    }
}
