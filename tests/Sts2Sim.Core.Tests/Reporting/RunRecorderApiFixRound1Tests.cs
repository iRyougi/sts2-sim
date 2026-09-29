using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

[Collection("ModelDb")]
public sealed class RunRecorderApiFixRound1Tests : IDisposable
{
    public RunRecorderApiFixRound1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void RecordEndTurn_UsesExplicitFinalSnapshotsForDrawOnlyTurn()
    {
        RecorderContext context = StartCombat("recorder-draw-only-final");
        CardModel card = context.Player.Deck.Cards[0];
        context.Recorder.RecordDraw(card);
        PlayerSnapshot finalPlayer = context.PlayerPre with
        {
            Energy = 2,
            Hand = [card.Id.ToString()],
            DrawPileSize = context.PlayerPre.DrawPileSize - 1,
        };
        EnemySnapshot[] finalEnemies =
        [
            context.EnemiesPre[0] with { Intent = "Attack 12" },
        ];

        context.Recorder.RecordEndTurn(finalPlayer, finalEnemies);
        FinishCombat(context, finalPlayer, finalEnemies, EmptyRewards());

        TurnRecord turn = Assert.Single(context.Recorder.BuildCombatLog(context.CombatId).Turns);
        Assert.Equal(2, turn.PlayerPost.Energy);
        Assert.Equal([card.Id.ToString()], turn.PlayerPost.Hand);
        Assert.Equal("Attack 12", Assert.Single(turn.EnemiesPost).Intent);
    }

    [Fact]
    public void EndCombat_UsesExplicitFinalSnapshotsForOpenWinningTurn()
    {
        RecorderContext context = StartCombat("recorder-open-final");
        PlayerSnapshot finalPlayer = context.PlayerPre with { Hp = 69, Block = 4 };
        EnemySnapshot[] finalEnemies =
        [
            context.EnemiesPre[0] with { Hp = 0, Intent = "None" },
        ];

        FinishCombat(context, finalPlayer, finalEnemies, EmptyRewards());

        TurnRecord turn = Assert.Single(context.Recorder.BuildCombatLog(context.CombatId).Turns);
        Assert.Equal(69, turn.PlayerPost.Hp);
        Assert.Equal(4, turn.PlayerPost.Block);
        Assert.Equal(0, Assert.Single(turn.EnemiesPost).Hp);
        Assert.DoesNotContain(turn.Actions, action => action is EndTurnAction);
    }

    [Theory]
    [InlineData(1, 3, 3, false)]
    [InlineData(3, 3, 0, false)]
    [InlineData(0, 3, 3, true)]
    public void RecordCardPlay_UsesGrossEnergySpentFromCardPlay(
        int grossEnergySpent,
        int energyBefore,
        int energyAfter,
        bool isAutoPlay)
    {
        RecorderContext context = StartCombat($"recorder-energy-{grossEnergySpent}-{isAutoPlay}");
        CardModel card = context.Player.Deck.Cards[0];
        var cardPlay = new CardPlay
        {
            Card = card,
            Player = context.Player,
            Target = context.Enemy,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo(
                EnergySpent: grossEnergySpent,
                EnergyValue: grossEnergySpent,
                StarsSpent: 0,
                StarValue: 0),
            IsAutoPlay = isAutoPlay,
            PlayIndex = 0,
            PlayCount = 1,
        };
        PlayerSnapshot before = context.PlayerPre with { Energy = energyBefore };
        PlayerSnapshot after = context.PlayerPre with { Energy = energyAfter };

        context.Recorder.RecordCardPlay(
            cardPlay,
            before,
            after,
            context.EnemiesPre,
            context.EnemiesPre);
        context.Recorder.RecordEndTurn(after, context.EnemiesPre);
        FinishCombat(context, after, context.EnemiesPre, EmptyRewards());

        var action = Assert.IsType<PlayCardAction>(
            Assert.Single(context.Recorder.BuildCombatLog(context.CombatId).Turns).Actions[0]);
        Assert.Equal(grossEnergySpent, action.EnergyCost);
    }

    [Fact]
    public void EndCombat_CopiesNonEmptyRewardsBeforeCallerMutation()
    {
        RecorderContext context = StartCombat("recorder-rewards-copy");
        var cardsOffered = new List<string> { "CARD.STRIKE_REGENT" };
        var cardsTaken = new List<string> { "CARD.STRIKE_REGENT", "CARD.DEFEND_REGENT" };
        var relicsTaken = new List<string> { "RELIC.DIVINE_RIGHT", "RELIC.CIRCLET" };
        var potionsTaken = new List<string> { "POTION.STRENGTH_POTION", "POTION.FIRE_POTION" };
        var rewards = new CombatRewards(
            Gold: 25,
            CardsOffered: cardsOffered,
            CardTaken: "CARD.STRIKE_REGENT",
            RelicTaken: "RELIC.DIVINE_RIGHT",
            PotionTaken: "POTION.STRENGTH_POTION")
        {
            CardsTaken = cardsTaken,
            RelicsTaken = relicsTaken,
            PotionsTaken = potionsTaken,
        };

        FinishCombat(context, context.PlayerPre, context.EnemiesPre, rewards);
        cardsOffered.Add("CARD.DEFEND_REGENT");
        cardsTaken.Clear();
        relicsTaken.Clear();
        potionsTaken.Clear();

        CombatRewards recorded = context.Recorder.BuildCombatLog(context.CombatId).Rewards;
        Assert.Equal(25, recorded.Gold);
        Assert.Equal(["CARD.STRIKE_REGENT"], recorded.CardsOffered);
        Assert.Equal("CARD.STRIKE_REGENT", recorded.CardTaken);
        Assert.Equal("RELIC.DIVINE_RIGHT", recorded.RelicTaken);
        Assert.Equal("POTION.STRENGTH_POTION", recorded.PotionTaken);
        Assert.Equal(["CARD.STRIKE_REGENT", "CARD.DEFEND_REGENT"], recorded.CardsTaken);
        Assert.Equal(["RELIC.DIVINE_RIGHT", "RELIC.CIRCLET"], recorded.RelicsTaken);
        Assert.Equal(["POTION.STRENGTH_POTION", "POTION.FIRE_POTION"], recorded.PotionsTaken);
    }

    private static RecorderContext StartCombat(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        player.ResetCombatState();
        player.PopulateCombatState(runState.Rng.Shuffle);
        player.PlayerCombatState!.Energy = 3;
        var monster = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        Creature enemy = combatState.AddMonster(monster, CombatSide.Enemy, "front");
        monster.SetUpForCombat();
        monster.RollMove(combatState.Allies);
        var recorder = new RunRecorder();
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        string combatId = recorder.BeginCombat(
            runState,
            RoomType.Monster,
            "TrainingDummy",
            combatState);
        recorder.RecordTurnStart(combatState);
        return new RecorderContext(
            recorder,
            runState,
            player,
            enemy,
            combatId,
            SnapshotFactory.SnapshotPlayer(player),
            combatState.Enemies.Select(SnapshotFactory.SnapshotEnemy).ToArray());
    }

    private static void FinishCombat(
        RecorderContext context,
        PlayerSnapshot finalPlayer,
        IReadOnlyList<EnemySnapshot> finalEnemies,
        CombatRewards rewards)
    {
        context.Recorder.EndCombat(
            victory: true,
            finalPlayer,
            finalEnemies,
            rewards);
        context.Recorder.ExitFloor();
        context.Recorder.EndRun(
            won: true,
            floorsVisited: 1,
            finalHp: context.Player.Creature.CurrentHp);
    }

    private static CombatRewards EmptyRewards() => new(0, [], null, null, null);

    private sealed record RecorderContext(
        RunRecorder Recorder,
        RunState RunState,
        Player Player,
        Creature Enemy,
        string CombatId,
        PlayerSnapshot PlayerPre,
        EnemySnapshot[] EnemiesPre);
}
