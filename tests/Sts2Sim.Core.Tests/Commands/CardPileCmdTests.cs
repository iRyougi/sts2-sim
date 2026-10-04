namespace Sts2Sim.Core.Tests.Commands;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class CardPileCmdTests
{
    public CardPileCmdTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState) => RunState = runState;
        public IEnumerable<AbstractModel> IterateHookListeners() => Array.Empty<AbstractModel>();
        public IRunState RunState { get; }
        public IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> Allies => Array.Empty<Sts2Sim.Core.Entities.Creatures.Creature>();
        public IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> Enemies => Array.Empty<Sts2Sim.Core.Entities.Creatures.Creature>();
        public IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> Creatures => Array.Empty<Sts2Sim.Core.Entities.Creatures.Creature>();
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> HittableEnemies => Array.Empty<Sts2Sim.Core.Entities.Creatures.Creature>();
        public CombatSide CurrentSide { get; set; }
        public int RoundNumber { get; set; } = 1;
        public IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> GetOpponentsOf(Sts2Sim.Core.Entities.Creatures.Creature creature) => Array.Empty<Sts2Sim.Core.Entities.Creatures.Creature>();
        public IReadOnlyList<Sts2Sim.Core.Entities.Creatures.Creature> GetCreaturesOnSide(CombatSide side) => Array.Empty<Sts2Sim.Core.Entities.Creatures.Creature>();
        public bool ContainsCreature(Sts2Sim.Core.Entities.Creatures.Creature creature) => false;
        public bool IsLiveCombat() => true;
    }

    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;
        public RunRngSet Rng { get; } = new RunRngSet("card_pile_cmd_tests");
        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private static (Player player, ICombatState combatState) MakeSetup(params AbstractModel[] listeners)
    {
        var runState = new FakeRunState(listeners);
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.ResetCombatState();
        player.PopulateCombatState(new Rng(3uL));
        return (player, new FakeCombatState(runState));
    }

    [Fact]
    public async Task Draw_MovesCardsFromDrawPileToHand()
    {
        (Player player, ICombatState combatState) = MakeSetup();

        CardModel[] expected = player.PlayerCombatState!.DrawPile.Cards.Take(5).ToArray();

        IReadOnlyList<CardModel> drawn = await CardPileCmd.Draw(
            combatState,
            5,
            player,
            fromHandDraw: true);

        Assert.Equal(expected, drawn);
        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(5, player.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task Draw_ShouldDrawVetoReturnsEmptyWithoutMovingCards()
    {
        var gate = new DrawGateModel();
        (Player player, ICombatState combatState) = MakeSetup(gate);
        CardModel first = player.PlayerCombatState!.DrawPile.Cards[0];

        IReadOnlyList<CardModel> blocked = await CardPileCmd.Draw(
            combatState,
            1,
            player,
            fromHandDraw: false);
        IReadOnlyList<CardModel> handDraw = await CardPileCmd.Draw(
            combatState,
            1,
            player,
            fromHandDraw: true);

        Assert.Empty(blocked);
        Assert.Same(first, Assert.Single(handDraw));
        Assert.Equal(new[] { false, true }, gate.FromHandDrawArguments);
    }

    [Fact]
    public async Task Draw_StopsAtMaxHandSize()
    {
        (Player player, ICombatState combatState) = MakeSetup();

        await CardPileCmd.Draw(combatState, 20, player, fromHandDraw: true);

        Assert.Equal(10, player.PlayerCombatState!.Hand.Cards.Count);
    }

    // 原版 CardModel.CompareTo：同 Id 再按升级等级（普通牌在前）（#32）。重洗用的 StableShuffle 因此与初始顺序无关，
    // 且等于"按 (Id, 升级等级) 排序后同种子 Fisher–Yates"。只按 Id 比较时同名的普通牌与升级牌会互换位置。
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StableShuffle_OrdersSameIdCardsByUpgradeLevelLikeNative(bool reversedInput)
    {
        var runState = new FakeRunState();
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        CardModel Make<T>(bool upgraded) where T : CardModel
        {
            var card = (CardModel)ModelDb.Card<T>().MutableClone();
            card.AssignOwner(player);
            if (upgraded) card.Upgrade();
            return card;
        }

        var cards = new List<CardModel>
        {
            Make<StrikeRegent>(true), Make<DefendRegent>(false), Make<StrikeRegent>(false),
            Make<DefendRegent>(true), Make<StrikeRegent>(false), Make<DefendRegent>(false),
        };
        List<CardModel> input = reversedInput ? Enumerable.Reverse(cards).ToList() : cards.ToList();
        const ulong seed = 20260925;

        List<CardModel> mirror = input.OrderBy(card => card.Id).ThenBy(card => card.CurrentUpgradeLevel).ToList();
        var mirrorRng = new Rng(seed);
        for (int n = mirror.Count - 1; n > 0; n--)
        {
            int k = mirrorRng.NextInt(n + 1);
            (mirror[k], mirror[n]) = (mirror[n], mirror[k]);
        }

        input.StableShuffle(new Rng(seed));

        Assert.Equal(
            mirror.Select(card => (card.Id.Entry, card.IsUpgraded)),
            input.Select(card => (card.Id.Entry, card.IsUpgraded)));
    }

    [Fact]
    public async Task ShuffleIfNecessary_ReshufflesDiscardIntoDraw_WhenDrawPileEmpty()
    {
        (Player player, ICombatState combatState) = MakeSetup();
        await CardPileCmd.Draw(combatState, 10, player, fromHandDraw: true);
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
        Assert.Empty(player.PlayerCombatState.DrawPile.Cards);
        Assert.Equal(10, player.PlayerCombatState.DiscardPile.Cards.Count);

        await CardPileCmd.ShuffleIfNecessary(combatState, player);

        Assert.Equal(10, player.PlayerCombatState.DrawPile.Cards.Count);
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public void Add_MovesCardBetweenPiles()
    {
        (Player player, ICombatState _) = MakeSetup();
        CardModel card = player.PlayerCombatState!.DrawPile.Cards[0];

        CardPileCmd.Add(card, PileType.Exhaust);

        Assert.DoesNotContain(player.PlayerCombatState.DrawPile.Cards, c => ReferenceEquals(c, card));
        Assert.Contains(player.PlayerCombatState.ExhaustPile.Cards, c => ReferenceEquals(c, card));
    }

    [Theory]
    [InlineData("single")]
    [InlineData("batch")]
    [InlineData("reorder")]
    [InlineData("full-hand")]
    [InlineData("partly-removed")]
    [InlineData("ending")]
    public async Task AddAsync_ReportsOnlyActualPileChanges(string scenario)
    {
        (Player player, CombatState combat, PileObserverCard observer) = await MakeSemanticContext(scenario);
        var piles = player.PlayerCombatState!;
        CardModel first = AddSemanticCard<StrikeRegent>(player,
            scenario is "reorder" or "full-hand" ? PileType.Discard : PileType.Hand);
        CardModel? second = null;
        if (scenario is "batch" or "partly-removed" or "reorder")
            second = AddSemanticCard<DefendRegent>(player,
                scenario == "reorder" ? PileType.Discard : PileType.Hand);
        if (scenario == "partly-removed") second!.Pile!.RemoveInternal(second);
        if (scenario == "full-hand")
            for (int i = 0; i < CardPile.MaxCardsInHand; i++) AddSemanticCard<StrikeRegent>(player, PileType.Hand);
        if (scenario == "ending") player.Creature.SetCurrentHpInternal(0);
        int rngBefore = player.RunState.Rng.Shuffle.Counter;
        PileType destination = scenario == "full-hand" ? PileType.Hand : PileType.Discard;
        CardPilePosition position = scenario is "single" or "ending" ? CardPilePosition.Random : CardPilePosition.Bottom;

        IReadOnlyList<CardPileAddResult> results = scenario is "batch" or "partly-removed"
            ? await CardPileCmd.AddAsync(combat, new[] { first, second! }, destination, position)
            : new[] { await CardPileCmd.AddAsync(combat, first, destination, position) };

        Assert.Equal(rngBefore + (scenario == "single" ? 1 : 0), player.RunState.Rng.Shuffle.Counter);
        Assert.Equal(scenario != "ending", results[0].Success);
        Assert.Same(scenario == "ending" ? piles.Hand : piles.DiscardPile, first.Pile);
        bool notified = scenario is "single" or "batch" or "partly-removed";
        CardModel[] expectedCards = scenario == "batch" ? new[] { first, second! } : new[] { first };
        IEnumerable<(CardModel Card, string Phase)> expectedChanges = notified
            ? expectedCards.SelectMany(card => new[] { (card, "normal"), (card, "late") })
            : Array.Empty<(CardModel, string)>();
        Assert.Equal(expectedChanges, observer.Changes.Select(change => (change.Card, change.Phase)));
        Assert.All(observer.Changes, change =>
        {
            Assert.Equal(PileType.Hand, change.OldPile);
            Assert.Equal(PileType.Discard, change.CurrentPile);
        });
        if (scenario == "batch")
        {
            Assert.All(results, result => Assert.True(result.Success));
            Assert.All(observer.DiscardSnapshots, snapshot => Assert.Equal(expectedCards, snapshot));
        }
        if (scenario == "partly-removed")
        {
            Assert.False(results[1].Success);
            Assert.Null(second!.Pile);
        }
        if (scenario == "reorder") Assert.Equal(new[] { second!, first }, piles.DiscardPile.Cards);
        if (scenario == "full-hand") Assert.Equal(CardPile.MaxCardsInHand, piles.Hand.Cards.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exhaust_ResultAndCallbacksFollowNative(bool ending)
    {
        (Player player, CombatState combat, PileObserverCard observer) = await MakeSemanticContext("exhaust-" + ending);
        CardModel card = AddSemanticCard<StrikeRegent>(player, PileType.Hand);
        int historyBefore = combat.SemanticHistory.CardsExhaustedThisCombat;
        if (ending) player.Creature.SetCurrentHpInternal(0);

        CardPileAddResult? result = await CardPileCmd.Exhaust(combat, card, causedByEthereal: true);

        if (ending)
        {
            Assert.Null(result);
            Assert.Same(player.PlayerCombatState!.Hand, card.Pile);
            Assert.Empty(observer.Changes);
            Assert.Empty(observer.ExhaustHistoryCounts);
            Assert.Equal(historyBefore, combat.SemanticHistory.CardsExhaustedThisCombat);
        }
        else
        {
            Assert.True(result.HasValue && result.Value.Success);
            Assert.Same(player.PlayerCombatState!.ExhaustPile, card.Pile);
            Assert.Equal(new[] { "normal", "late", "exhaust" }, observer.EventOrder);
            Assert.All(observer.ChangeHistoryCounts, count => Assert.Equal(historyBefore, count));
            Assert.Equal(new[] { historyBefore + 1 }, observer.ExhaustHistoryCounts);
            CardModel ethereal = AddSemanticCard<AscendersBane>(player, PileType.Hand);
            observer.VetoEthereal = true;
            await CombatEngine.DoTurnEndAsync(combat, player);
            Assert.Same(player.PlayerCombatState.Hand, ethereal.Pile);
            Assert.Equal(historyBefore + 1, combat.SemanticHistory.CardsExhaustedThisCombat);
        }
    }

    private static async Task<(Player, CombatState, PileObserverCard)> MakeSemanticContext(string seed)
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
        var run = new RunState("399-piles-" + seed, new Sts2Sim.Core.Content.Acts.Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var room = new Sts2Sim.Core.Rooms.CombatRoom(() =>
            (MonsterModel)ModelDb.Monster<Sts2Sim.Core.Models.Monsters.WanderingGrunt>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
            CardPileCmd.Add(card, PileType.Draw);
        var observer = (PileObserverCard)new PileObserverCard().MutableClone();
        observer.AssignOwner(player);
        player.PlayerCombatState.DrawPile.AddInternal(observer);
        return (player, room.Engine.State, observer);
    }

    private static CardModel AddSemanticCard<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (CardModel)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Get(pile, player)!.AddInternal(card);
        return card;
    }
}

internal sealed class PileObserverCard : CardModel
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    protected override int CanonicalEnergyCost => -1;
    public bool VetoEthereal { get; set; }
    public List<(CardModel Card, string Phase, PileType OldPile, PileType? CurrentPile)> Changes { get; } = new();
    public List<CardModel[]> DiscardSnapshots { get; } = new();
    public List<string> EventOrder { get; } = new();
    public List<int> ChangeHistoryCounts { get; } = new();
    public List<int> ExhaustHistoryCounts { get; } = new();
    public override bool ShouldEtherealTrigger(CardModel card) => !VetoEthereal;
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
        Record(card, oldPileType, "normal");
    public override Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
        Record(card, oldPileType, "late");
    private Task Record(CardModel card, PileType oldPile, string phase)
    {
        Changes.Add((card, phase, oldPile, card.Pile?.Type));
        DiscardSnapshots.Add(Owner.PlayerCombatState!.DiscardPile.Cards.ToArray());
        EventOrder.Add(phase);
        ChangeHistoryCounts.Add(((CombatState)CombatState!).SemanticHistory.CardsExhaustedThisCombat);
        return Task.CompletedTask;
    }
    public override Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        EventOrder.Add("exhaust");
        ExhaustHistoryCounts.Add(((CombatState)CombatState!).SemanticHistory.CardsExhaustedThisCombat);
        return Task.CompletedTask;
    }
}

file sealed class DrawGateModel : AbstractModel
{
    public override bool ShouldReceiveCombatHooks => true;

    public List<bool> FromHandDrawArguments { get; } = new();

    public override bool ShouldDraw(Player player, bool fromHandDraw)
    {
        FromHandDrawArguments.Add(fromHandDraw);
        return fromHandDraw;
    }
}
