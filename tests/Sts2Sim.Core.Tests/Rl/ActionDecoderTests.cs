using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rl;

public class ActionDecoderTests : IDisposable
{
    public ActionDecoderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt), typeof(HardyBrute), typeof(ActOneGuardian),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void DecodeMapChoice_ReturnsOptionAtPosition()
    {
        var runState = new RunState("action-decoder-map", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.OrderBy(p => p.coord.col).ToList();
        int actionIndex = ActionSpaceLayout.MapPointIndex(1);

        MapPoint chosen = ActionDecoder.DecodeMapChoice(actionIndex, options);

        Assert.Same(options[1], chosen);
    }

    [Fact]
    public void DecodeMapChoice_ThrowsForNonMapPointIndex()
    {
        var runState = new RunState("action-decoder-map-invalid", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        IReadOnlyList<MapPoint> options = runState.Map.StartingMapPoint.Children.ToList();

        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeMapChoice(actionIndex: 0, options));
    }

    [Fact]
    public void DecodeEventChoice_ReturnsOptionAtPosition()
    {
        IReadOnlyList<EventOption> options =
        [
            new EventOption("GAIN", () => Task.CompletedTask),
            new EventOption("LEAVE", () => Task.CompletedTask),
        ];

        EventOption chosen = ActionDecoder.DecodeEventChoice(ActionSpaceLayout.EventIndex(1), options);

        Assert.Same(options[1], chosen);
    }

    [Fact]
    public void DecodeEventChoice_ThrowsForOutOfRangeOrOverflowOptions()
    {
        IReadOnlyList<EventOption> options =
        [
            new EventOption("GAIN", () => Task.CompletedTask),
            new EventOption("LEAVE", () => Task.CompletedTask),
        ];
        IReadOnlyList<EventOption> overflow = Enumerable.Range(0, ActionSpaceLayout.MaxEventChoices + 1)
            .Select(index => new EventOption($"OPTION_{index}", () => Task.CompletedTask))
            .ToList();

        Assert.Throws<ArgumentException>(
            () => ActionDecoder.DecodeEventChoice(ActionSpaceLayout.MapPointIndex(0), options));
        Assert.Throws<ArgumentException>(
            () => ActionDecoder.DecodeEventChoice(ActionSpaceLayout.EventIndex(2), options));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActionDecoder.DecodeEventChoice(ActionSpaceLayout.EventIndex(0), overflow));
    }

    private static (RunState runState, CombatState combatState, Player player) NewCombat(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        var monster = (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone();
        combatState.AddMonster(monster, CombatSide.Enemy);
        player.ResetCombatState();
        player.PopulateCombatState(runState.Rng.Shuffle);
        foreach (CardModel card in player.PlayerCombatState!.DrawPile.Cards.Take(5).ToList())
        {
            player.PlayerCombatState.Hand.AddInternal(card);
        }
        player.PlayerCombatState.Energy = 99;
        return (runState, combatState, player);
    }

    private static int EnsureCardInHand<TCard>(Player player)
        where TCard : CardModel
    {
        int index = player.PlayerCombatState!.Hand.Cards.ToList().FindIndex(card => card is TCard);
        if (index >= 0)
        {
            return index;
        }

        CardModel card = player.PlayerCombatState.DrawPile.Cards.First(card => card is TCard);
        player.PlayerCombatState.Hand.AddInternal(card);
        return player.PlayerCombatState.Hand.Cards.Count - 1;
    }

    [Fact]
    public void DecodeCombatChoice_EndTurnIndex_ReturnsEndTurn()
    {
        (_, CombatState combatState, _) = NewCombat("action-decoder-combat-a");

        CombatDecision decision = ActionDecoder.DecodeCombatChoice(ActionSpaceLayout.EndTurnIndex, combatState);

        Assert.IsType<CombatDecision.EndTurn>(decision);
    }

    [Fact]
    public void DecodeCombatChoice_PlayCardIndex_ReturnsPlayCardWithMatchingHandCardAndTarget()
    {
        (_, CombatState combatState, Player player) = NewCombat("action-decoder-combat-b");
        int strikeHandIndex = EnsureCardInHand<StrikeRegent>(player);
        int actionIndex = ActionSpaceLayout.PlayCardIndex(strikeHandIndex, enemyIndex: 0);

        CombatDecision decision = ActionDecoder.DecodeCombatChoice(actionIndex, combatState);

        var playCard = Assert.IsType<CombatDecision.PlayCard>(decision);
        Assert.Same(player.PlayerCombatState!.Hand.Cards[strikeHandIndex], playCard.Card);
        Assert.Same(combatState.HittableEnemies[0], playCard.Target);
    }

    [Fact]
    public void DecodeCombatChoice_NonEnemyTargetCardAtEnemyIndexZero_ReturnsNullTarget()
    {
        (_, CombatState combatState, Player player) = NewCombat("action-decoder-combat-c");
        int defendHandIndex = EnsureCardInHand<DefendRegent>(player);
        int actionIndex = ActionSpaceLayout.PlayCardIndex(defendHandIndex, enemyIndex: 0);

        CombatDecision decision = ActionDecoder.DecodeCombatChoice(actionIndex, combatState);

        var playCard = Assert.IsType<CombatDecision.PlayCard>(decision);
        Assert.Same(player.PlayerCombatState!.Hand.Cards[defendHandIndex], playCard.Card);
        Assert.Null(playCard.Target);
    }

    [Fact]
    public void DecodeCombatChoice_ThrowsForNonEnemyTargetCardAtOtherEnemyIndex()
    {
        (_, CombatState combatState, Player player) = NewCombat("action-decoder-combat-d");
        int defendHandIndex = EnsureCardInHand<DefendRegent>(player);
        int actionIndex = ActionSpaceLayout.PlayCardIndex(defendHandIndex, enemyIndex: 1);

        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeCombatChoice(actionIndex, combatState));
    }

    [Fact]
    public void DecodeCombatChoice_ThrowsForHandIndexOutsideCurrentHand()
    {
        (_, CombatState combatState, Player player) = NewCombat("action-decoder-combat-e");
        int actionIndex = ActionSpaceLayout.PlayCardIndex(player.PlayerCombatState!.Hand.Cards.Count, enemyIndex: 0);

        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeCombatChoice(actionIndex, combatState));
    }

    [Fact]
    public void DecodeCombatChoice_ThrowsForEnemyIndexOutsideHittableEnemies()
    {
        (_, CombatState combatState, Player player) = NewCombat("action-decoder-combat-f");
        int strikeHandIndex = EnsureCardInHand<StrikeRegent>(player);
        int actionIndex = ActionSpaceLayout.PlayCardIndex(strikeHandIndex, enemyIndex: 1);

        Assert.Throws<ArgumentException>(() => ActionDecoder.DecodeCombatChoice(actionIndex, combatState));
    }
}
