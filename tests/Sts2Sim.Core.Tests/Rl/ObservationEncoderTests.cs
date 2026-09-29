using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rl;

public class ObservationEncoderTests : IDisposable
{
    public ObservationEncoderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt), typeof(HardyBrute), typeof(ActOneGuardian),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    private static (RunState runState, Player player) NewRunState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static (RunState runState, Player player, CombatState combatState, Creature enemy) NewCombat(string seed)
    {
        (RunState runState, Player player) = NewRunState(seed);
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        var monster = (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone();
        Creature enemy = combatState.AddMonster(monster, CombatSide.Enemy);
        player.ResetCombatState();
        player.PopulateCombatState(runState.Rng.Shuffle);
        foreach (CardModel card in player.PlayerCombatState!.DrawPile.Cards.Take(5).ToList())
        {
            player.PlayerCombatState.Hand.AddInternal(card);
        }
        monster.RunRng = runState.Rng;
        monster.Rng = new Sts2Sim.Core.Random.Rng(runState.Rng.Seed);
        monster.SetUpForCombat();
        monster.RollMove(combatState.Allies);
        return (runState, player, combatState, enemy);
    }

    [Fact]
    public void EncodeMapDecision_DecisionTypeIsMapPoint()
    {
        (RunState runState, _) = NewRunState("obs-map-a");
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.ToList();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeMapDecision(runState, options);

        Assert.Equal(DecisionType.MapPoint, snapshot.DecisionType);
    }

    [Fact]
    public void EncodeMapDecision_LegalActionMask_HasExactlyOptionsCountTrueEntries()
    {
        (RunState runState, _) = NewRunState("obs-map-b");
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.ToList();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeMapDecision(runState, options);

        Assert.Equal(ActionSpaceLayout.TotalActions, snapshot.LegalActionMask.Count);
        Assert.Equal(options.Count, snapshot.LegalActionMask.Count(legal => legal));
    }

    [Fact]
    public void EncodeMapDecision_CandidatesMatchLegalMapPointSlots()
    {
        (RunState runState, _) = NewRunState("obs-map-c");
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.ToList();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeMapDecision(runState, options);

        Assert.Equal(options.Count, snapshot.Candidates.Count);
        foreach (CandidateSlot candidate in snapshot.Candidates)
        {
            Assert.True(ActionSpaceLayout.TryDecodeMapPoint(candidate.SlotIndex, out _));
            Assert.True(snapshot.LegalActionMask[candidate.SlotIndex]);
        }
    }

    [Fact]
    public void EncodeMapDecision_PlayerFieldReflectsCreatureState()
    {
        (RunState runState, Player player) = NewRunState("obs-map-d");
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.ToList();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeMapDecision(runState, options);

        Assert.Equal(player.Creature.CurrentHp, snapshot.Player.CurrentHp);
        Assert.Equal(player.Creature.MaxHp, snapshot.Player.MaxHp);
        Assert.Equal(player.Gold, snapshot.Player.Gold);
    }

    [Fact]
    public void EncodeMapDecision_DeckViewListsFullDeckWithoutOrderGuarantee()
    {
        (RunState runState, Player player) = NewRunState("obs-map-e");
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.ToList();

        ObservationSnapshot snapshot = ObservationEncoder.EncodeMapDecision(runState, options);

        Assert.Equal(player.Deck.Cards.Count, snapshot.DeckView.Count);
    }

    [Fact]
    public void EncodeEventDecision_UsesOrderedEventSlotsAndKeys()
    {
        (RunState runState, _) = NewRunState("obs-event-a");
        IReadOnlyList<EventOption> options =
        [
            new EventOption("GAIN", () => Task.CompletedTask),
            new EventOption("LEAVE", () => Task.CompletedTask),
        ];

        ObservationSnapshot snapshot = ObservationEncoder.EncodeEventDecision(runState, options);

        Assert.Equal(DecisionType.Event, snapshot.DecisionType);
        Assert.Equal(
            [ActionSpaceLayout.EventIndex(0), ActionSpaceLayout.EventIndex(1)],
            snapshot.Candidates.Select(candidate => candidate.SlotIndex));
        Assert.Equal(["GAIN", "LEAVE"], snapshot.Candidates.Select(candidate => candidate.Label));
        Assert.Equal(2, snapshot.LegalActionMask.Count(isLegal => isLegal));
        Assert.True(snapshot.LegalActionMask[ActionSpaceLayout.EventIndex(0)]);
        Assert.True(snapshot.LegalActionMask[ActionSpaceLayout.EventIndex(1)]);
        Assert.False(snapshot.LegalActionMask[ActionSpaceLayout.EventIndex(2)]);
    }

    [Fact]
    public void EncodeEventDecision_ThrowsWhenOptionsExceedActionSpaceCapacity()
    {
        (RunState runState, _) = NewRunState("obs-event-overflow");
        IReadOnlyList<EventOption> options = Enumerable.Range(0, ActionSpaceLayout.MaxEventChoices + 1)
            .Select(index => new EventOption($"OPTION_{index}", () => Task.CompletedTask))
            .ToList();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ObservationEncoder.EncodeEventDecision(runState, options));
    }

    [Fact]
    public void EncodeCombatDecision_DecisionTypeIsCombat()
    {
        (RunState runState, _, CombatState combatState, _) = NewCombat("obs-combat-a");

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCombatDecision(runState, combatState);

        Assert.Equal(DecisionType.Combat, snapshot.DecisionType);
    }

    [Fact]
    public void EncodeCombatDecision_EnemiesIncludeHpAndIntent()
    {
        (RunState runState, _, CombatState combatState, Creature enemy) = NewCombat("obs-combat-b");

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCombatDecision(runState, combatState);

        Assert.Single(snapshot.Enemies);
        Assert.Equal(enemy.CurrentHp, snapshot.Enemies[0].CurrentHp);
        Assert.NotEqual(IntentType.Unknown.ToString(), snapshot.Enemies[0].IntentType);
    }

    [Fact]
    public void EncodeCombatDecision_HandCardsAreFlaggedByCanPlay()
    {
        (RunState runState, Player player, CombatState combatState, _) = NewCombat("obs-combat-c");
        player.PlayerCombatState!.Energy = 0;

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCombatDecision(runState, combatState);

        Assert.Equal(player.PlayerCombatState.Hand.Cards.Count, snapshot.Hand.Count);
        Assert.All(snapshot.Hand, card => Assert.False(card.CanPlay));
    }

    [Fact]
    public void EncodeCombatDecision_MaskIncludesEndTurnSlot()
    {
        (RunState runState, _, CombatState combatState, _) = NewCombat("obs-combat-d");

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCombatDecision(runState, combatState);

        Assert.True(snapshot.LegalActionMask[ActionSpaceLayout.EndTurnIndex]);
    }

    [Fact]
    public void EncodeCombatDecision_PlayableHandCardWithAnyEnemyTarget_UnlocksPlayCardSlotForHittableEnemy()
    {
        (RunState runState, Player player, CombatState combatState, _) = NewCombat("obs-combat-e");
        player.PlayerCombatState!.Energy = 99;

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCombatDecision(runState, combatState);

        int strikeHandIndex = player.PlayerCombatState.Hand.Cards
            .ToList()
            .FindIndex(card => card.TargetType == TargetType.AnyEnemy);
        Assert.True(strikeHandIndex >= 0);
        int expectedSlot = ActionSpaceLayout.PlayCardIndex(strikeHandIndex, enemyIndex: 0);
        Assert.True(snapshot.LegalActionMask[expectedSlot]);
    }

    [Fact]
    public void EncodeCombatDecision_PlayableNonEnemyTargetCard_UsesOnlyEnemyIndexZeroSlot()
    {
        (RunState runState, Player player, CombatState combatState, _) = NewCombat("obs-combat-f");
        player.PlayerCombatState!.Energy = 99;

        if (!player.PlayerCombatState.Hand.Cards.Any(card => card is DefendRegent))
        {
            CardModel defend = player.PlayerCombatState.DrawPile.Cards.First(card => card is DefendRegent);
            player.PlayerCombatState.Hand.AddInternal(defend);
        }

        var secondMonster = (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone();
        combatState.AddMonster(secondMonster, CombatSide.Enemy);

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCombatDecision(runState, combatState);

        int defendHandIndex = player.PlayerCombatState.Hand.Cards.ToList().FindIndex(card => card is DefendRegent);
        int expectedSlot = ActionSpaceLayout.PlayCardIndex(defendHandIndex, enemyIndex: 0);
        Assert.True(snapshot.LegalActionMask[expectedSlot]);
        Assert.Contains(snapshot.Candidates, candidate => candidate.SlotIndex == expectedSlot);

        for (int enemyIndex = 1; enemyIndex < ActionSpaceLayout.MaxEnemies; enemyIndex++)
        {
            int unexpectedSlot = ActionSpaceLayout.PlayCardIndex(defendHandIndex, enemyIndex);
            Assert.False(snapshot.LegalActionMask[unexpectedSlot]);
            Assert.DoesNotContain(snapshot.Candidates, candidate => candidate.SlotIndex == unexpectedSlot);
        }
    }}
