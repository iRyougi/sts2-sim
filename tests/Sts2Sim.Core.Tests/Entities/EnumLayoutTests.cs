namespace Sts2Sim.Core.Tests.Entities;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

public class EnumLayoutTests
{
    [Fact]
    public void RunRngType_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration",
            "CombatPotionGeneration", "CombatCardSelection", "CombatEnergyCosts",
            "CombatTargets", "MonsterAi", "Niche", "CombatOrbs", "TreasureRoomRelics",
        }, Enum.GetNames<RunRngType>());
    }

    [Fact]
    public void PlayerRngType_HasGameOrder()
    {
        Assert.Equal(new[] { "Rewards", "Shops", "Transformations" }, Enum.GetNames<PlayerRngType>());
    }

    [Fact]
    public void RoomType_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "Unassigned", "Monster", "Elite", "Boss", "Treasure", "Shop", "Event", "RestSite", "Map",
        }, Enum.GetNames<RoomType>());
    }

    [Fact]
    public void CardRarity_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "None", "Basic", "Common", "Uncommon", "Rare", "Ancient",
            "Event", "Token", "Status", "Curse", "Quest",
        }, Enum.GetNames<CardRarity>());
    }

    [Fact]
    public void CardRarityOddsType_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "None", "RegularEncounter", "EliteEncounter", "BossEncounter", "Shop", "Uniform",
        }, Enum.GetNames<CardRarityOddsType>());
    }

    [Fact]
    public void AscensionLevel_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "None", "SwarmingElites", "WearyTraveler", "Poverty", "TightBelt",
            "AscendersBane", "Inflation", "Scarcity", "ToughEnemies", "DeadlyEnemies", "DoubleBoss",
        }, Enum.GetNames<AscensionLevel>());
    }

    [Fact]
    public void CardType_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Attack", "Skill", "Power", "Status", "Curse", "Quest" }, Enum.GetNames<CardType>());
    }

    [Fact]
    public void TargetType_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "None", "Self", "AnyEnemy", "AllEnemies", "RandomEnemy", "AnyPlayer",
            "AnyAlly", "AllAllies", "TargetedNoCreature", "Osty",
        }, Enum.GetNames<TargetType>());
    }

    [Fact]
    public void PileType_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Draw", "Hand", "Discard", "Exhaust", "Play", "Deck" }, Enum.GetNames<PileType>());
    }

    [Fact]
    public void CardPilePosition_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Bottom", "Top", "Random" }, Enum.GetNames<CardPilePosition>());
    }

    [Fact]
    public void UnplayableReason_HasGameValues()
    {
        Assert.Equal(0, (int)UnplayableReason.None);
        Assert.Equal(2, (int)UnplayableReason.HasUnplayableKeyword);
        Assert.Equal(4, (int)UnplayableReason.BlockedByHook);
        Assert.Equal(8, (int)UnplayableReason.BlockedByCardLogic);
        Assert.Equal(0x10, (int)UnplayableReason.EnergyCostTooHigh);
        Assert.Equal(0x20, (int)UnplayableReason.StarCostTooHigh);
        Assert.Equal(0x40, (int)UnplayableReason.NoLivingAllies);
    }

    [Fact]
    public void ValueProp_HasGameValues()
    {
        Assert.Equal(2, (int)ValueProp.Unblockable);
        Assert.Equal(4, (int)ValueProp.Unpowered);
        Assert.Equal(8, (int)ValueProp.Move);
        Assert.Equal(0x10, (int)ValueProp.SkipHurtAnim);
    }

    [Fact]
    public void PowerType_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Buff", "Debuff" }, Enum.GetNames<PowerType>());
    }

    [Fact]
    public void PowerStackType_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Counter", "Single" }, Enum.GetNames<PowerStackType>());
    }

    [Fact]
    public void PowerInstanceType_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Instanced", "InstancedPerApplier" }, Enum.GetNames<PowerInstanceType>());
    }

    [Fact]
    public void HpLossHookPhase_HasGameValues()
    {
        Assert.Equal(0, (int)HpLossHookPhase.None);
        Assert.Equal(1, (int)HpLossHookPhase.BeforeOsty);
        Assert.Equal(2, (int)HpLossHookPhase.AfterOsty);
        Assert.Equal(3, (int)HpLossHookPhase.All);
    }

    [Fact]
    public void ModifyDamageHookType_HasGameValues()
    {
        Assert.Equal(0, (int)ModifyDamageHookType.None);
        Assert.Equal(2, (int)ModifyDamageHookType.Additive);
        Assert.Equal(4, (int)ModifyDamageHookType.Multiplicative);
        Assert.Equal(8, (int)ModifyDamageHookType.Cap);
        Assert.Equal(0xE, (int)ModifyDamageHookType.All);
    }

    [Fact]
    public void PlayerTurnPhase_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Start", "AutoPrePlay", "Play", "AutoPostPlay", "End" }, Enum.GetNames<PlayerTurnPhase>());
    }

    [Fact]
    public void IntentType_HasGameOrder()
    {
        Assert.Equal(new[]
        {
            "Attack", "Buff", "Debuff", "DebuffStrong", "Defend", "Escape", "Heal", "Hidden",
            "Summon", "Sleep", "Stun", "StatusCard", "CardDebuff", "DeathBlow", "Unknown",
        }, Enum.GetNames<IntentType>());
    }

    [Fact]
    public void CombatSide_HasGameOrder()
    {
        Assert.Equal(new[] { "None", "Player", "Enemy" }, Enum.GetNames<CombatSide>());
    }
}
