using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class TemporaryStatPowerTests : IDisposable
{
    public TemporaryStatPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(TemporaryStrengthPower), typeof(TemporaryDexterityPower),
            typeof(StrengthPower), typeof(DexterityPower), typeof(ArtifactPower),
            typeof(FlexPotionPower), typeof(FeedingFrenzyPower), typeof(CoordinatePower),
            typeof(SpeedPotionPower), typeof(AnticipatePower), typeof(HelicalDartPower), typeof(FadePower),
            typeof(PiercingWailPower), typeof(ShacklingPotionPower), typeof(DarkShacklesPower),
            typeof(DyingStarPower), typeof(MonarchsGazeStrengthDownPower), typeof(CrushUnderPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(typeof(FlexPotionPower))]
    [InlineData(typeof(FeedingFrenzyPower))]
    [InlineData(typeof(CoordinatePower))]
    public async Task TemporaryStrengthPower_AddsItsAmountToPoweredDamage_ThenClearsOnlyAtOwnersSideTurnEnd(Type powerType)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("temp-strength");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        PowerModel power = (await PowerCmd.Apply(room.Engine.State, powerType, player.Creature, 5m, applier: null, cardSource: null))!;
        Assert.Equal(5, player.Creature.GetPower<StrengthPower>()!.Amount);
        await PowerCmd.Apply(room.Engine.State, powerType, player.Creature, 3m, null, null);
        Assert.Equal(8, player.Creature.GetPower<StrengthPower>()!.Amount);
        await PowerCmd.ModifyAmount(room.Engine.State, power, -3m, null, null);
        Assert.Equal(5, player.Creature.GetPower<StrengthPower>()!.Amount);
        int hpBefore = enemy.CurrentHp;

        await AddToHand<StrikeRegent>(player).PlayAsync(enemy);

        Assert.Equal(hpBefore - 11, enemy.CurrentHp);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, new[] { enemy });
        Assert.Single(player.Creature.Powers.OfType<TemporaryStrengthPower>());
        Assert.Equal(5m, power.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, new[] { player.Creature });
        Assert.Empty(player.Creature.Powers.OfType<TemporaryStrengthPower>());
        Assert.Null(player.Creature.GetPower<StrengthPower>());
    }

    [Theory]
    [InlineData(typeof(SpeedPotionPower))]
    [InlineData(typeof(AnticipatePower))]
    [InlineData(typeof(HelicalDartPower))]
    [InlineData(typeof(FadePower))]
    public async Task TemporaryDexterityPower_AddsItsAmountToPoweredBlock_ThenClearsOnlyAtOwnersSideTurnEnd(Type powerType)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("temp-dexterity");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        PowerModel power = (await PowerCmd.Apply(room.Engine.State, powerType, player.Creature, 5m, applier: null, cardSource: null))!;
        Assert.Equal(5, player.Creature.GetPower<DexterityPower>()!.Amount);
        await PowerCmd.Apply(room.Engine.State, powerType, player.Creature, 3m, null, null);
        Assert.Equal(8, player.Creature.GetPower<DexterityPower>()!.Amount);
        await PowerCmd.ModifyAmount(room.Engine.State, power, -3m, null, null);
        Assert.Equal(5, player.Creature.GetPower<DexterityPower>()!.Amount);
        int blockBefore = player.Creature.Block;

        await AddToHand<DefendRegent>(player).PlayAsync(target: null);

        Assert.Equal(blockBefore + 10, player.Creature.Block);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, new[] { enemy });
        Assert.Single(player.Creature.Powers.OfType<TemporaryDexterityPower>());
        Assert.Equal(5m, power.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, new[] { player.Creature });
        Assert.Empty(player.Creature.Powers.OfType<TemporaryDexterityPower>());
        Assert.Null(player.Creature.GetPower<DexterityPower>());
    }

    [Fact]
    public async Task TemporaryDexterityPower_FollowsOwnedSourceAndExcludesUnpoweredBlock()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("temp-dexterity-source");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<TemporaryDexterityPower>(room.Engine.State, player.Creature, 5m, applier: null, cardSource: null);
        int playerBlockBefore = player.Creature.Block;

        await CreatureCmd.GainBlock(room.Engine.State, player.Creature, 5m, ValueProp.Unpowered, null, null);

        Assert.Equal(playerBlockBefore + 5, player.Creature.Block);

        DefendRegent playerOwnedSource = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        playerOwnedSource.AssignOwner(player);
        int enemyBlockBefore = enemy.Block;

        await CreatureCmd.GainBlock(room.Engine.State, enemy, 5m, ValueProp.Move, playerOwnedSource, null);

        Assert.Equal(enemyBlockBefore + 10, enemy.Block);
    }

    [Theory]
    [InlineData(typeof(PiercingWailPower))]
    [InlineData(typeof(ShacklingPotionPower))]
    [InlineData(typeof(DarkShacklesPower))]
    [InlineData(typeof(DyingStarPower))]
    [InlineData(typeof(MonarchsGazeStrengthDownPower))]
    [InlineData(typeof(CrushUnderPower))]
    public async Task NegativeMarkers_ApplyRealStrengthAndArtifactBlocksMarker(Type powerType)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"negative-{powerType.Name}");
        var state = room.Engine.State;
        Creature enemy = state.HittableEnemies.Single();
        await PowerCmd.Apply<StrengthPower>(state, enemy, 2m, null, null);
        await PowerCmd.Apply<ArtifactPower>(state, enemy, 1m, null, null);
        Assert.Null(await PowerCmd.Apply(state, powerType, enemy, 5m, player.Creature, null));
        Assert.Null(enemy.GetPower<ArtifactPower>());
        Assert.Equal(2, enemy.GetPower<StrengthPower>()!.Amount);

        PowerModel marker = (await PowerCmd.Apply(state, powerType, enemy, 5m, player.Creature, null))!;
        Assert.Equal(PowerType.Debuff, marker.Type);
        Assert.False(marker.AllowNegative);
        Assert.Equal(5, marker.Amount);
        Assert.Equal(-3, enemy.GetPower<StrengthPower>()!.Amount);
        await PowerCmd.Apply(state, powerType, enemy, 2m, player.Creature, null);
        Assert.Equal(7, marker.Amount);
        Assert.Equal(-5, enemy.GetPower<StrengthPower>()!.Amount);
        await Hook.AfterSideTurnEnd(state, CombatSide.Player, [player.Creature]);
        Assert.Contains(marker, enemy.Powers);
        await Hook.AfterSideTurnEnd(state, CombatSide.Enemy, [enemy]);
        Assert.DoesNotContain(marker, enemy.Powers);
        Assert.Equal(2, enemy.GetPower<StrengthPower>()!.Amount);
    }

    [Theory]
    [InlineData(typeof(FlexPotionPower), typeof(StrengthPower))]
    [InlineData(typeof(SpeedPotionPower), typeof(DexterityPower))]
    public async Task Artifact_BlocksPositiveTemporaryStatRecovery(Type markerType, Type statType)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"recovery-{markerType.Name}");
        var state = room.Engine.State;
        await PowerCmd.Apply(state, markerType, player.Creature, 5m, null, null);
        await PowerCmd.Apply<ArtifactPower>(state, player.Creature, 1m, null, null);
        await Hook.AfterSideTurnEnd(state, CombatSide.Player, [player.Creature]);
        Assert.DoesNotContain(player.Creature.Powers, power => power.GetType() == markerType);
        Assert.Null(player.Creature.GetPower<ArtifactPower>());
        Assert.Equal(5, Assert.Single(player.Creature.Powers, power => power.GetType() == statType).Amount);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
