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
using Sts2Sim.Core.Models.Relics;
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
        Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
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

        ExtraTurnPower power = Assert.Single(player.Creature.Powers.OfType<ExtraTurnPower>());
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
        Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
        Assert.Equal(5, player.Creature.Block);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<ToricToughnessPower>()).Amount);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
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

        ExtraTurnPower crossPower = Assert.Single(targetPlayer.Creature.Powers.OfType<ExtraTurnPower>());
        Assert.Equal(30, targetPlayer.Creature.CurrentHp - targetHpBefore);
        Assert.Equal(ownerHpBefore, potionOwner.Creature.CurrentHp);
        Assert.Equal(1, crossPower.Amount);
        Assert.Same(potionOwner.Creature, crossPower.Applier);
        Assert.Empty(potionOwner.Creature.Powers.OfType<ExtraTurnPower>());
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
        Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
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
        Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
    }

    [Fact]
    public async Task StackedAmountTwo_SchedulesExactlyTwoExtraTurnsBeforeEnemyTurn()
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-ambergris-stacked");
        await PowerCmd.Apply<ExtraTurnPower>(
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
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<ExtraTurnPower>()).Amount);
        Assert.Equal(hpBefore, player.Creature.CurrentHp);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Equal(roundBefore, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 2, player.PlayerCombatState.TurnNumber);
        Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
        Assert.Equal(hpBefore, player.Creature.CurrentHp);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(roundBefore + 1, room.Engine.State.RoundNumber);
        Assert.Equal(turnBefore + 3, player.PlayerCombatState.TurnNumber);
        Assert.True(player.Creature.CurrentHp < hpBefore);
    }

    [Theory]
    [InlineData("potion_only")]
    [InlineData("paels_eye_only")]
    [InlineData("both_no_manual_play")]
    [InlineData("both_with_manual_play")]
    [InlineData("earring_turn1_suppression")]
    public async Task ExtraTurnSelection_OrdersPhasesAndConsumesOnlySelectedSource(string scenario)
    {
        var runState = new RunState($"extra-turn-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        bool hasPaelsEye = scenario is not "potion_only";
        bool hasPotion = scenario is not "paels_eye_only";
        bool hasEarring = scenario == "earring_turn1_suppression";
        if (hasEarring)
            await RelicCmd.Obtain(ModelDb.Relic<WhisperingEarring>(), player);
        if (hasPaelsEye)
            await RelicCmd.Obtain(ModelDb.Relic<PaelsEye>(), player);

        var room = new CombatRoom(() => (Task11RerollTrackingMonster)ModelDb.Monster<Task11RerollTrackingMonster>().MutableClone());
        await room.Enter(runState);

        if (hasEarring)
        {
            // Keep hand deterministic after WhisperingEarring's Turn-1 opening autoplays,
            // and keep non-empty draw cards so Turn 2 draw does not reshuffle turn1Marker out of DiscardPile.
            foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(p => p.Cards).ToList())
                CardPileCmd.Remove(card);
            for (int i = 0; i < 10; i++)
            {
                var drawFiller = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
                drawFiller.AssignOwner(player);
                CardPileCmd.Add(drawFiller, PileType.Draw);
            }
        }

        if (hasPotion)
        {
            Ambergris potion = (Ambergris)player.AddPotionInternal(ModelDb.Potion<Ambergris>());
            await PotionCmd.Use(potion, player, player.Creature);
        }

        DefendRegent turn1Marker = AddToHand<DefendRegent>(player);
        switch (scenario)
        {
            case "potion_only":
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());
                Assert.Contains(turn1Marker, player.PlayerCombatState.DiscardPile.Cards);

                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, room.Engine.State.RoundNumber);
                Assert.Equal(3, player.PlayerCombatState.TurnNumber);
                break;

            case "paels_eye_only":
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Contains(turn1Marker, player.PlayerCombatState.ExhaustPile.Cards);

                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, room.Engine.State.RoundNumber);
                Assert.Equal(3, player.PlayerCombatState.TurnNumber);
                break;

            case "both_no_manual_play":
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Contains(turn1Marker, player.PlayerCombatState.ExhaustPile.Cards);
                Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<ExtraTurnPower>()).Amount);

                DefendRegent manualPlayTurn2 = AddToHand<DefendRegent>(player);
                await room.Engine.PlayCardAsync(player, manualPlayTurn2, null);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(3, player.PlayerCombatState.TurnNumber);
                Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());

                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, room.Engine.State.RoundNumber);
                Assert.Equal(4, player.PlayerCombatState.TurnNumber);
                break;

            case "both_with_manual_play":
                DefendRegent manualPlayTurn1 = AddToHand<DefendRegent>(player);
                await room.Engine.PlayCardAsync(player, manualPlayTurn1, null);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Contains(turn1Marker, player.PlayerCombatState.DiscardPile.Cards);
                Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());

                DefendRegent turn2Marker = AddToHand<DefendRegent>(player);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(3, player.PlayerCombatState.TurnNumber);
                Assert.Contains(turn2Marker, player.PlayerCombatState.ExhaustPile.Cards);

                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, room.Engine.State.RoundNumber);
                Assert.Equal(4, player.PlayerCombatState.TurnNumber);
                break;

            case "earring_turn1_suppression":
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Contains(turn1Marker, player.PlayerCombatState.DiscardPile.Cards);
                Assert.Empty(player.Creature.Powers.OfType<ExtraTurnPower>());

                DefendRegent turn2MarkerEarring = AddToHand<DefendRegent>(player);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, room.Engine.State.RoundNumber);
                Assert.Equal(3, player.PlayerCombatState.TurnNumber);
                Assert.Contains(turn2MarkerEarring, player.PlayerCombatState.ExhaustPile.Cards);

                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, room.Engine.State.RoundNumber);
                Assert.Equal(4, player.PlayerCombatState.TurnNumber);
                break;
        }
    }

    private static T AddToHand<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
