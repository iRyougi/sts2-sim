namespace Sts2Sim.Core.Tests.Odds;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

public class CardRarityOddsTests
{
    private static readonly AscensionManager A0 = new(0);
    private static readonly AscensionManager A7 = new(AscensionLevel.Scarcity);

    // 用"孪生 Rng"预知下一个 NextFloat，从而精确断言 roll 结果
    private static (CardRarityOdds odds, Rng twin) Make(AscensionManager asc, float initialValue = -0.05f)
    {
        var rng = new Rng(4242u);
        var twin = new Rng(4242u);
        return (new CardRarityOdds(initialValue, rng, asc), twin);
    }

    [Fact]
    public void InitialOffset_IsBaseRarityOffset()
    {
        var odds = new CardRarityOdds(new Rng(1u), A0);
        Assert.Equal(-0.05f, odds.CurrentValue);
    }

    [Fact]
    public void Roll_NonRare_GrowsOffsetByGrowth_CappedAtMax()
    {
        var (odds, _) = Make(A0, initialValue: 0.395f);
        // A0 growth = 0.01；无论 roll 出什么非稀有结果，offset 变为 min(0.395+0.01, 0.4)
        // seed 4242 的第一个 NextFloat ≈ 大概率 > rare 阈值（rare 基础 0.03 + 0.395）——
        // 若碰巧 roll 出 Rare 则重置为 -0.05，两个分支都断言：
        CardRarity r = odds.Roll(CardRarityOddsType.RegularEncounter);
        if (r == CardRarity.Rare)
        {
            Assert.Equal(-0.05f, odds.CurrentValue);
        }
        else
        {
            Assert.Equal(0.4f, odds.CurrentValue, 5);
        }
    }

    [Fact]
    public void Roll_Rare_ResetsOffset()
    {
        // offset 拉满 0.4 + rare 基础 0.03 = 0.43：只要 NextFloat < 0.43 就出 Rare。
        // 循环直到命中 Rare，验证重置行为
        var (odds, _) = Make(A0, initialValue: 0.4f);
        for (int i = 0; i < 200; i++)
        {
            CardRarity r = odds.Roll(CardRarityOddsType.RegularEncounter);
            if (r == CardRarity.Rare)
            {
                Assert.Equal(-0.05f, odds.CurrentValue);
                return;
            }
            odds.OverrideCurrentValue(0.4f); // 保持高 offset 直到命中
        }
        Assert.Fail("200 次高 offset roll 未出 Rare，实现大概率有误");
    }

    [Fact]
    public void BossEncounter_AlwaysRare_AndIgnoresOffset()
    {
        var (odds, _) = Make(A0, initialValue: -0.05f);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(CardRarity.Rare, odds.Roll(CardRarityOddsType.BossEncounter));
        }
    }

    [Fact]
    public void RollWithoutChangingFutureOdds_DoesNotMutateState()
    {
        var (odds, _) = Make(A0);
        float before = odds.CurrentValue;
        odds.RollWithoutChangingFutureOdds(CardRarityOddsType.RegularEncounter);
        Assert.Equal(before, odds.CurrentValue);
    }

    [Fact]
    public void Roll_MatchesManualThresholdComputation()
    {
        // 完全复算：用孪生 Rng 取相同的 NextFloat，手工套用阈值公式
        var (odds, twin) = Make(A0);
        float offset = odds.CurrentValue;
        float roll = twin.NextFloat();
        float rareThreshold = 0.03f + offset;              // A0 RegularRareOdds = 0.03
        float uncommonThreshold = 0.37f + rareThreshold;   // regularUncommonOdds = 0.37
        CardRarity expected = roll < rareThreshold ? CardRarity.Rare
            : roll < uncommonThreshold ? CardRarity.Uncommon
            : CardRarity.Common;
        Assert.Equal(expected, odds.Roll(CardRarityOddsType.RegularEncounter));
    }

    [Fact]
    public void ScarcityAscension_UsesReducedOdds()
    {
        var odds = new CardRarityOdds(new Rng(1u), A7);
        // A7: growth 0.01→0.005；出非稀有后 offset = -0.05 + 0.005 = -0.045
        CardRarity r = odds.Roll(CardRarityOddsType.RegularEncounter);
        if (r != CardRarity.Rare)
        {
            Assert.Equal(-0.045f, odds.CurrentValue, 5);
        }
        else
        {
            Assert.Equal(-0.05f, odds.CurrentValue);
        }
    }
}
