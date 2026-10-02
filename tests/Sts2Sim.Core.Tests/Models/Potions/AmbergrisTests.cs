using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public sealed class AmbergrisTests : IDisposable
{
    public AmbergrisTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[] { typeof(Task11RerollTrackingMonster) }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task UseOutOfCombat_HealsHalfMaxHpRoundedDown_WithoutPower()
    {
        Player player = CreatePlayer("task11-ambergris-out");
        player.Creature.LoseHpInternal(65m, ValueProp.Unpowered);
        int hpBefore = player.Creature.CurrentHp;
        Ambergris potion = (Ambergris)player.AddPotionInternal(ModelDb.Potion<Ambergris>());

        await PotionCmd.Use(potion, player, player.Creature);

        Assert.Equal(37, player.Creature.CurrentHp - hpBefore);
        Assert.Empty(player.Creature.Powers.OfType<AmbergrisPower>());
        Assert.Equal(PotionRarity.Event, potion.Rarity);
        Assert.Equal(PotionUsage.AnyTime, potion.Usage);
        Assert.Equal(TargetType.AnyPlayer, potion.TargetType);
    }

    [Fact]
    public async Task UseInLiveCombat_AppliesPower_AndSchedulesOneExtraTurnBeforeEnemies()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-ambergris-live");
        player.Creature.LoseHpInternal(30m, ValueProp.Unpowered);
        Ambergris potion = (Ambergris)player.AddPotionInternal(ModelDb.Potion<Ambergris>());
        int hpBeforePotion = player.Creature.CurrentHp;
        int roundBefore = room.Engine.State.RoundNumber;
        int turnBefore = player.PlayerCombatState!.TurnNumber;

        await PotionCmd.Use(potion, player, player.Creature);

        AmbergrisPower power = Assert.Single(player.Creature.Powers.OfType<AmbergrisPower>());
        Assert.Equal(30, player.Creature.CurrentHp - hpBeforePotion);
        Assert.Equal(1, power.Amount);
        Assert.Same(player.Creature, power.Applier);
        Assert.DoesNotContain(potion, player.PotionSlots);

        ToricToughness toric = Task11CombatTestSupport.AddToHand<ToricToughness>(player);
        await toric.PlayAsync(target: null);
        Assert.Equal(5, player.Creature.Block);

        int hpBeforeExtra = player.Creature.CurrentHp;
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Equal(roundBefore, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 1, player.PlayerCombatState.TurnNumber);
        Assert.Equal(player.MaxEnergy, player.PlayerCombatState.Energy);
        Assert.Equal(hpBeforeExtra, player.Creature.CurrentHp);
        Assert.Empty(player.Creature.Powers.OfType<AmbergrisPower>());
        Assert.Equal(5, player.Creature.Block);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<ToricToughnessPower>()).Amount);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Empty(player.Creature.Powers.OfType<AmbergrisPower>());
        Assert.Equal(roundBefore + 1, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 2, player.PlayerCombatState.TurnNumber);
        Assert.True(player.Creature.CurrentHp < hpBeforeExtra);

        var crossRun = new RunState("task11-ambergris-cross-source", new Overgrowth());
        Player potionOwner = Player.CreateForNewRun(ModelDb.Character<Regent>(), crossRun);
        Player targetPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), crossRun);
        crossRun.AddPlayer(potionOwner);
        crossRun.AddPlayer(targetPlayer);
        var crossRoom = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await crossRoom.Enter(crossRun);
        targetPlayer.Creature.LoseHpInternal(30m, ValueProp.Unpowered);
        int targetHpBefore = targetPlayer.Creature.CurrentHp;
        int ownerHpBefore = potionOwner.Creature.CurrentHp;
        PotionModel crossPotion = potionOwner.AddPotionInternal(ModelDb.Potion<Ambergris>());

        await PotionCmd.Use(crossPotion, potionOwner, targetPlayer.Creature);

        AmbergrisPower crossPower = Assert.Single(targetPlayer.Creature.Powers.OfType<AmbergrisPower>());
        Assert.Equal(30, targetPlayer.Creature.CurrentHp - targetHpBefore);
        Assert.Equal(ownerHpBefore, potionOwner.Creature.CurrentHp);
        Assert.Equal(1, crossPower.Amount);
        Assert.Same(potionOwner.Creature, crossPower.Applier);
        Assert.Empty(potionOwner.Creature.Powers.OfType<AmbergrisPower>());
        Assert.DoesNotContain(crossPotion, potionOwner.PotionSlots);
        Assert.True(crossPower.ShouldTakeExtraTurn(targetPlayer));
        Assert.False(crossPower.ShouldTakeExtraTurn(potionOwner));
    }

    [Fact]
    public async Task ExtraTurn_DoesNotRerollEnemyIntentOrConsumeMonsterAiRng()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync<Task11RerollTrackingMonster>(
            "task11-ambergris-intent");
        var enemy = Assert.Single(room.Engine.State.Enemies).Monster!;
        await enemy.PerformMove();
        int rngCounterBefore = room.Engine.State.RunState.Rng.MonsterAi.Counter;
        int stateLogCountBefore = enemy.MoveStateMachine!.StateLog.Count;
        Ambergris potion = (Ambergris)player.AddPotionInternal(ModelDb.Potion<Ambergris>());
        await PotionCmd.Use(potion, player, player.Creature);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Equal(rngCounterBefore, room.Engine.State.RunState.Rng.MonsterAi.Counter);
        Assert.Equal(stateLogCountBefore, enemy.MoveStateMachine.StateLog.Count);
        Assert.Empty(player.Creature.Powers.OfType<AmbergrisPower>());
    }
    [Fact]
    public async Task UseWithAttachedButEndedCombat_HealsWithoutApplyingPower()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-ambergris-ended");
        player.Creature.LoseHpInternal(30m, ValueProp.Unpowered);
        Creature enemy = Assert.Single(room.Engine.State.Enemies);
        enemy.LoseHpInternal(enemy.CurrentHp, ValueProp.Unpowered);
        Assert.True(room.Engine.CheckWinCondition());
        Assert.False(room.Engine.State.IsLiveCombat());
        int hpBefore = player.Creature.CurrentHp;
        Ambergris potion = (Ambergris)player.AddPotionInternal(ModelDb.Potion<Ambergris>());

        await PotionCmd.Use(potion, player, player.Creature);

        Assert.Equal(30, player.Creature.CurrentHp - hpBefore);
        Assert.Empty(player.Creature.Powers.OfType<AmbergrisPower>());
    }

    [Fact]
    public async Task StackedAmountTwo_SchedulesExactlyTwoExtraTurnsBeforeEnemyTurn()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-ambergris-stacked");
        await PowerCmd.Apply<AmbergrisPower>(
            room.Engine.State,
            player.Creature,
            2m,
            player.Creature,
            cardSource: null);
        int roundBefore = room.Engine.State.RoundNumber;
        int turnBefore = player.PlayerCombatState!.TurnNumber;
        int hpBefore = player.Creature.CurrentHp;

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Equal(roundBefore, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 1, player.PlayerCombatState.TurnNumber);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<AmbergrisPower>()).Amount);
        Assert.Equal(hpBefore, player.Creature.CurrentHp);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Equal(roundBefore, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 2, player.PlayerCombatState.TurnNumber);
        Assert.Empty(player.Creature.Powers.OfType<AmbergrisPower>());
        Assert.Equal(hpBefore, player.Creature.CurrentHp);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(roundBefore + 1, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 3, player.PlayerCombatState.TurnNumber);
        Assert.True(player.Creature.CurrentHp < hpBefore);
    }
    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
