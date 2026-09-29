using Sts2Sim.Core.Rl;

namespace Sts2Sim.Core.Tests.Rl;

public class ActionSpaceExpansionTests
{
    [Fact]
    public void NewDecisionRanges_AreContiguousAfterTheStableCombatAndMapRanges()
    {
        Assert.Equal(58, ActionSpaceLayout.RewardBase);
        Assert.Equal(62, ActionSpaceLayout.ShopBase);
        Assert.Equal(332, ActionSpaceLayout.RestSiteBase);
        Assert.Equal(590, ActionSpaceLayout.EventBase);
        Assert.Equal(593, ActionSpaceLayout.CardSelectionBase);
        Assert.Equal(849, ActionSpaceLayout.PlayerTargetBase);
        Assert.Equal(913, ActionSpaceLayout.CardSelectionConfirmIndex);
        Assert.Equal(914, ActionSpaceLayout.CardSelectionCancelIndex);
        // The legacy block still ends at 916; custom-event slots are appended past it, so no
        // pre-existing action index moved.
        Assert.Equal(915, ActionSpaceLayout.CookRestSiteIndex);
        Assert.Equal(916, ActionSpaceLayout.CustomEventBase);
    }

    [Fact]
    public void ShopCapacity_EqualsItsStockedDynamicAndLeaveComponents()
    {
        Assert.Equal(7, ActionSpaceLayout.MaxShopCardChoices);
        Assert.Equal(3, ActionSpaceLayout.MaxShopRelicChoices);
        Assert.Equal(3, ActionSpaceLayout.MaxShopPotionChoices);
        Assert.Equal(256, ActionSpaceLayout.MaxDeckBackedChoices);

        int stockedChoices =
            ActionSpaceLayout.MaxShopCardChoices +
            ActionSpaceLayout.MaxShopRelicChoices +
            ActionSpaceLayout.MaxShopPotionChoices;
        Assert.Equal(13, stockedChoices);
        Assert.Equal(
            1,
            ActionSpaceLayout.MaxShopChoices - stockedChoices - ActionSpaceLayout.MaxDeckBackedChoices);
        Assert.Equal(270, ActionSpaceLayout.MaxShopChoices);
    }

    [Theory]
    [InlineData(0, 58)]
    [InlineData(3, 61)]
    public void RewardIndex_EncodesPosition(int position, int expected) =>
        Assert.Equal(expected, ActionSpaceLayout.RewardIndex(position));

    [Theory]
    [InlineData(0, 62)]
    [InlineData(269, 331)]
    public void ShopIndex_EncodesPosition(int position, int expected) =>
        Assert.Equal(expected, ActionSpaceLayout.ShopIndex(position));

    [Theory]
    [InlineData(0, 332)]
    [InlineData(257, 589)]
    public void RestSiteIndex_EncodesPosition(int position, int expected) =>
        Assert.Equal(expected, ActionSpaceLayout.RestSiteIndex(position));

    [Theory]
    [InlineData(0, 590)]
    [InlineData(2, 592)]
    public void EventIndex_EncodesPosition(int position, int expected) =>
        Assert.Equal(expected, ActionSpaceLayout.EventIndex(position));

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void RewardIndex_ThrowsForPositionsOutsideItsFixedRange(int position) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionSpaceLayout.RewardIndex(position));

    [Theory]
    [InlineData(-1)]
    [InlineData(270)]
    public void ShopIndex_ThrowsForPositionsOutsideItsFixedRange(int position) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionSpaceLayout.ShopIndex(position));

    [Theory]
    [InlineData(-1)]
    [InlineData(258)]
    public void RestSiteIndex_ThrowsForPositionsOutsideItsFixedRange(int position) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionSpaceLayout.RestSiteIndex(position));

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void EventIndex_ThrowsForPositionsOutsideItsFixedRange(int position) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionSpaceLayout.EventIndex(position));

    [Theory]
    [InlineData(58, true, 0)]
    [InlineData(61, true, 3)]
    [InlineData(57, false, 0)]
    [InlineData(62, false, 0)]
    public void TryDecodeReward_AcceptsOnlyItsOwnRange(int actionIndex, bool expected, int expectedPosition)
    {
        bool decoded = ActionSpaceLayout.TryDecodeReward(actionIndex, out int position);

        Assert.Equal(expected, decoded);
        if (expected)
        {
            Assert.Equal(expectedPosition, position);
        }
    }

    [Theory]
    [InlineData(62, true, 0)]
    [InlineData(331, true, 269)]
    [InlineData(61, false, 0)]
    [InlineData(332, false, 0)]
    public void TryDecodeShop_AcceptsOnlyItsOwnRange(int actionIndex, bool expected, int expectedPosition)
    {
        bool decoded = ActionSpaceLayout.TryDecodeShop(actionIndex, out int position);

        Assert.Equal(expected, decoded);
        if (expected)
        {
            Assert.Equal(expectedPosition, position);
        }
    }

    [Theory]
    [InlineData(332, true, 0)]
    [InlineData(589, true, 257)]
    [InlineData(331, false, 0)]
    [InlineData(590, false, 0)]
    public void TryDecodeRestSite_AcceptsOnlyItsOwnRange(int actionIndex, bool expected, int expectedPosition)
    {
        bool decoded = ActionSpaceLayout.TryDecodeRestSite(actionIndex, out int position);

        Assert.Equal(expected, decoded);
        if (expected)
        {
            Assert.Equal(expectedPosition, position);
        }
    }

    [Theory]
    [InlineData(590, true, 0)]
    [InlineData(592, true, 2)]
    [InlineData(589, false, 0)]
    [InlineData(593, false, 0)]
    public void TryDecodeEvent_AcceptsOnlyItsOwnRange(int actionIndex, bool expected, int expectedPosition)
    {
        bool decoded = ActionSpaceLayout.TryDecodeEvent(actionIndex, out int position);

        Assert.Equal(expected, decoded);
        if (expected)
        {
            Assert.Equal(expectedPosition, position);
        }
    }
}
