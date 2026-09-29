namespace Sts2Sim.Core.Tests.Helpers;

using Sts2Sim.Core.Helpers;

public class StringHelperTests
{
    // 黄金值：用游戏反编译代码中的算法原样计算（见设计文档 v2 第 6 节）
    [Theory]
    [InlineData("", 757602046)]
    [InlineData("act_1_map", 1794753407)]
    [InlineData("up_front", -1234747045)]
    [InlineData("shuffle", -1986686621)]
    [InlineData("unknown_map_point", -398231356)]
    [InlineData("combat_card_generation", -1289540940)]
    [InlineData("combat_potion_generation", 848987331)]
    [InlineData("combat_card_selection", 371871864)]
    [InlineData("combat_energy_costs", -1516337938)]
    [InlineData("combat_targets", 1525931235)]
    [InlineData("monster_ai", 1703902611)]
    [InlineData("niche", 497466721)]
    [InlineData("combat_orbs", 1247949129)]
    [InlineData("treasure_room_relics", -1894164228)]
    [InlineData("rewards", -459048038)]
    [InlineData("shops", -1875171877)]
    [InlineData("transformations", -131945376)]
    public void GetDeterministicHashCodeOld_MatchesGolden(string input, int expected)
    {
        Assert.Equal(expected, StringHelper.GetDeterministicHashCodeOld(input));
    }

    [Fact]
    public void GetDeterministicHashCode_IsXxHash64()
    {
        // System.IO.Hashing 与游戏同库同调用(UTF8, seed 0),此处锁定 wrapper 行为
        Assert.Equal(
            System.IO.Hashing.XxHash64.HashToUInt64(System.Text.Encoding.UTF8.GetBytes("act_1_map"), 0L),
            StringHelper.GetDeterministicHashCode("act_1_map"));
        Assert.Equal(
            System.IO.Hashing.XxHash64.HashToUInt64(System.Text.Encoding.UTF8.GetBytes(""), 0L),
            StringHelper.GetDeterministicHashCode(""));
        // 中文等多字节 UTF8 路径
        Assert.Equal(
            System.IO.Hashing.XxHash64.HashToUInt64(System.Text.Encoding.UTF8.GetBytes("储君"), 0L),
            StringHelper.GetDeterministicHashCode("储君"));
    }

    [Fact]
    public void GetDeterministicHashCode_StableAcrossCalls_AndDiffersBetweenInputs()
    {
        Assert.Equal(StringHelper.GetDeterministicHashCode("shuffle"), StringHelper.GetDeterministicHashCode("shuffle"));
        Assert.NotEqual(StringHelper.GetDeterministicHashCode("shuffle"), StringHelper.GetDeterministicHashCode("rewards"));
    }

    // 覆盖 RunRngType/PlayerRngType 全部枚举名 → 流名的转换
    [Theory]
    [InlineData("UpFront", "up_front")]
    [InlineData("Shuffle", "shuffle")]
    [InlineData("UnknownMapPoint", "unknown_map_point")]
    [InlineData("CombatCardGeneration", "combat_card_generation")]
    [InlineData("CombatPotionGeneration", "combat_potion_generation")]
    [InlineData("CombatCardSelection", "combat_card_selection")]
    [InlineData("CombatEnergyCosts", "combat_energy_costs")]
    [InlineData("CombatTargets", "combat_targets")]
    [InlineData("MonsterAi", "monster_ai")]
    [InlineData("Niche", "niche")]
    [InlineData("CombatOrbs", "combat_orbs")]
    [InlineData("TreasureRoomRelics", "treasure_room_relics")]
    [InlineData("Rewards", "rewards")]
    [InlineData("Shops", "shops")]
    [InlineData("Transformations", "transformations")]
    public void SnakeCase_MatchesGameBehavior(string input, string expected)
    {
        Assert.Equal(expected, StringHelper.SnakeCase(input));
    }
}
