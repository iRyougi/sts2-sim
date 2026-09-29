using Sts2Sim.Core.Rl;

namespace Sts2Sim.Core.Tests.Rl;

public class ActionSpaceLayoutTests
{
    [Fact]
    public void TotalActions_EqualsSumOfAllSlotRanges()
    {
        int actual =
            (ActionSpaceLayout.MaxHand * ActionSpaceLayout.MaxEnemies) +
            1 +
            ActionSpaceLayout.MaxMapChoices +
            ActionSpaceLayout.MaxRewardChoices +
            ActionSpaceLayout.MaxShopChoices +
            ActionSpaceLayout.MaxRestSiteChoices +
            ActionSpaceLayout.MaxEventChoices +
            ActionSpaceLayout.MaxCardSelectionChoices +
            ActionSpaceLayout.MaxPlayerTargetChoices +
            ActionSpaceLayout.MaxCustomEventChoices +
            ActionSpaceLayout.MaxRestSiteOverflowChoices +
            ActionSpaceLayout.MaxRewardOverflowChoices +
            3;

        Assert.Equal(ActionSpaceLayout.TotalActions, actual);
    }

    /// <summary>跨语言契约快照：Python 侧 python/sts2env/raw_env.py 的 _ACTION_COUNT 手动镜像
    /// 这个数值（Gym API 要求静态声明 action_space，无法运行时协商）。改动 TotalActions 时本测试
    /// 会失败，提醒同步修改 Python 侧常量——不要只改这里的期望值了事。</summary>
    [Fact]
    public void TotalActions_MatchesPythonActionCountSnapshot()
    {
        Assert.Equal(1077, ActionSpaceLayout.TotalActions);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 5)]
    [InlineData(0, 1, 1)]
    [InlineData(9, 4, 49)]
    public void PlayCardIndex_EncodesHandAndEnemyPosition(int handIndex, int enemyIndex, int expected)
    {
        Assert.Equal(expected, ActionSpaceLayout.PlayCardIndex(handIndex, enemyIndex));
    }

    [Fact]
    public void EndTurnIndex_IsImmediatelyAfterPlayCardRange()
    {
        Assert.Equal(ActionSpaceLayout.MaxHand * ActionSpaceLayout.MaxEnemies, ActionSpaceLayout.EndTurnIndex);
    }

    [Theory]
    [InlineData(0, 51)]
    [InlineData(6, 57)]
    public void MapPointIndex_EncodesPosition(int position, int expected)
    {
        Assert.Equal(expected, ActionSpaceLayout.MapPointIndex(position));
    }

    [Fact]
    public void TryDecodePlayCard_RoundTripsWithPlayCardIndex()
    {
        int index = ActionSpaceLayout.PlayCardIndex(handIndex: 3, enemyIndex: 2);

        bool decoded = ActionSpaceLayout.TryDecodePlayCard(index, out int handIndex, out int enemyIndex);

        Assert.True(decoded);
        Assert.Equal(3, handIndex);
        Assert.Equal(2, enemyIndex);
    }

    [Fact]
    public void TryDecodePlayCard_ReturnsFalse_ForEndTurnIndex()
    {
        bool decoded = ActionSpaceLayout.TryDecodePlayCard(ActionSpaceLayout.EndTurnIndex, out _, out _);

        Assert.False(decoded);
    }

    [Fact]
    public void TryDecodeMapPoint_RoundTripsWithMapPointIndex()
    {
        int index = ActionSpaceLayout.MapPointIndex(4);

        bool decoded = ActionSpaceLayout.TryDecodeMapPoint(index, out int position);

        Assert.True(decoded);
        Assert.Equal(4, position);
    }

    [Fact]
    public void TryDecodeMapPoint_ReturnsFalse_ForPlayCardIndex()
    {
        bool decoded = ActionSpaceLayout.TryDecodeMapPoint(0, out _);

        Assert.False(decoded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ActionSpaceLayout.EndTurnIndex)]
    [InlineData(ActionSpaceLayout.TotalActions)]
    public void TryDecodePlayCard_ReturnsFalse_OutsidePlayCardRange(int actionIndex)
    {
        Assert.False(ActionSpaceLayout.TryDecodePlayCard(actionIndex, out _, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ActionSpaceLayout.EndTurnIndex)]
    [InlineData(ActionSpaceLayout.TotalActions)]
    public void TryDecodeMapPoint_ReturnsFalse_OutsideMapPointRange(int actionIndex)
    {
        Assert.False(ActionSpaceLayout.TryDecodeMapPoint(actionIndex, out _));
    }}
