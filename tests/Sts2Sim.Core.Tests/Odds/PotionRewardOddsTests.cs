namespace Sts2Sim.Core.Tests.Odds;

using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

public class PotionRewardOddsTests
{
    [Fact]
    public void InitialValue_IsBaseOdds()
    {
        var odds = new PotionRewardOdds(new Rng(1u), NullOddsHooks.Instance);
        Assert.Equal(0.4f, odds.CurrentValue);
    }

    [Fact]
    public void Roll_SwingsCurrentValueByTenPercent()
    {
        var rng = new Rng(88u);
        var twin = new Rng(88u);
        var odds = new PotionRewardOdds(rng, NullOddsHooks.Instance);
        float before = odds.CurrentValue;
        float roll = twin.NextFloat();
        odds.Roll(RoomType.Monster);
        // 命中（roll < before）→ -0.1；未命中 → +0.1
        float expected = roll < before ? before - 0.1f : before + 0.1f;
        Assert.Equal(expected, odds.CurrentValue, 5);
    }

    [Fact]
    public void Roll_MatchesManualFormula_ForMonsterRoom()
    {
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var rng = new Rng(seed);
            var twin = new Rng(seed);
            var odds = new PotionRewardOdds(rng, NullOddsHooks.Instance);
            float current = odds.CurrentValue;
            float roll = twin.NextFloat();
            bool expected = roll < current;
            Assert.True(expected == odds.Roll(RoomType.Monster), $"seed={seed}, roll={roll}, threshold={current}");
            Assert.Equal(twin.Counter, rng.Counter);
        }
    }

    [Fact]
    public void Roll_EliteRoom_AddsHalfEliteBonusToThreshold()
    {
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var rng = new Rng(seed);
            var twin = new Rng(seed);
            var odds = new PotionRewardOdds(rng, NullOddsHooks.Instance);
            float current = odds.CurrentValue;
            float roll = twin.NextFloat();
            bool expected = roll < current + 0.25f * 0.5f;
            Assert.True(expected == odds.Roll(RoomType.Elite), $"seed={seed}, roll={roll}, threshold={current + 0.125f}");
            Assert.Equal(expected ? current - 0.1f : current + 0.1f, odds.CurrentValue, 5);
        }
    }

    [Fact]
    public void ForcedByHook_AlwaysReturnsTrue_WithoutDrawingOrChangingOdds()
    {
        var rng = new Rng(1u);
        var twin = new Rng(1u);
        var hooks = new ForcePotionHooks();
        var odds = new PotionRewardOdds(rng, hooks);
        float before = odds.CurrentValue;
        Assert.True(odds.Roll(RoomType.Monster));
        Assert.Equal(before, odds.CurrentValue);
        Assert.Equal(0, rng.Counter);
        hooks.Force = false;
        Assert.Equal(twin.NextFloat() < before, odds.Roll(RoomType.Monster));
        Assert.Equal(twin.Counter, rng.Counter);
    }

    private sealed class ForcePotionHooks : IOddsHooks
    {
        public bool Force { get; set; } = true;

        public bool ShouldForcePotionReward(RoomType roomType) => Force;

        public IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) => roomTypes;

        public float ModifyOddsIncreaseForUnrolledRoomType(RoomType roomType, float increase) => increase;
    }
}
