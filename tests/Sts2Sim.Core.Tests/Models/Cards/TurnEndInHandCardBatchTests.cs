using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

internal sealed class TurnEndDamageProbe : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public ValueProp? LastProps { get; private set; }

    public CardModel? LastCardSource { get; private set; }

    public Creature? LastDealer { get; private set; }

    public override Task BeforeDamageReceived(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        LastProps = props;
        LastCardSource = cardSource;
        LastDealer = dealer;
        return Task.CompletedTask;
    }
}

// 既有回合末效果、又是虚无的探针：原版 DoTurnEndCards 结算后把它消耗而不是放进弃牌堆。
internal sealed class EtherealTurnEndProbeCard : CardModel
{
    public int TriggerCount { get; private set; }

    public override CardType Type => CardType.Status;

    public override CardRarity Rarity => CardRarity.Status;

    public override TargetType TargetType => TargetType.None;

    public override int MaxUpgradeLevel => 0;

    protected override int CanonicalEnergyCost => -1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal };

    protected override bool HasTurnEndInHandEffect => true;

    protected override Task OnTurnEndInHand()
    {
        TriggerCount++;
        return Task.CompletedTask;
    }
}

internal sealed class HandMutationOnTurnEndCard : CardModel
{
    public CardModel CardToMove { get; set; } = null!;

    public override CardType Type => CardType.Status;

    public override CardRarity Rarity => CardRarity.Status;

    public override TargetType TargetType => TargetType.None;

    public override int MaxUpgradeLevel => 0;

    protected override int CanonicalEnergyCost => -1;

    protected override bool HasTurnEndInHandEffect => true;

    protected override Task OnTurnEndInHand()
    {
        CardPileCmd.Add(CardToMove, PileType.Discard);
        return Task.CompletedTask;
    }
}

// Plan 09-S1-399: isolates the source's dependency graph with a controllable effect pause.
// This is a source-derived contract probe, not a claim of a native-host gameplay replay.
internal sealed class PausedTurnEndProbeCard : CardModel
{
    public TaskCompletionSource Entered { get; } = new();
    public TaskCompletionSource Release { get; } = new();
    public int HandCountAfterPause { get; private set; }
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand()
    {
        Entered.SetResult();
        await Release.Task;
        HandCountAfterPause = Owner.PlayerCombatState!.Hand.Cards.Count;
    }
}

[Collection("ModelDb")]
public sealed class TurnEndInHandCardBatchTests : IDisposable
{
    public TurnEndInHandCardBatchTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<Type, CardType, CardRarity, int, CardKeyword[], bool> Cards => new()
    {
        { typeof(BadLuck), CardType.Curse, CardRarity.Curse, -1, new[] { CardKeyword.Eternal, CardKeyword.Unplayable }, false },
        { typeof(Debt), CardType.Curse, CardRarity.Curse, -1, new[] { CardKeyword.Unplayable }, true },
        { typeof(Decay), CardType.Curse, CardRarity.Curse, -1, new[] { CardKeyword.Unplayable }, true },
        { typeof(Regret), CardType.Curse, CardRarity.Curse, -1, new[] { CardKeyword.Unplayable }, true },
        { typeof(Beckon), CardType.Status, CardRarity.Status, 1, Array.Empty<CardKeyword>(), true },
        { typeof(Burn), CardType.Status, CardRarity.Status, -1, new[] { CardKeyword.Unplayable }, true },
        { typeof(Toxic), CardType.Status, CardRarity.Status, 1, new[] { CardKeyword.Exhaust }, true },
        { typeof(Wither), CardType.Status, CardRarity.Status, -1, new[] { CardKeyword.Unplayable }, true },
    };

    public static TheoryData<Type, int, ValueProp, bool> DamageCards => new()
    {
        { typeof(BadLuck), 13, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, true },
        { typeof(Decay), 2, ValueProp.Unpowered | ValueProp.Move, false },
        { typeof(Beckon), 6, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, true },
        { typeof(Burn), 2, ValueProp.Unpowered | ValueProp.Move, false },
        { typeof(Toxic), 5, ValueProp.Unpowered | ValueProp.Move, false },
        { typeof(Wither), 3, ValueProp.Unpowered | ValueProp.Move, false },
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public void MetadataAndGenerationGates_MatchAuthoritativeSource(
        Type cardType,
        CardType expectedType,
        CardRarity expectedRarity,
        int expectedCost,
        CardKeyword[] expectedKeywords,
        bool expectedModifierGeneration)
    {
        CardModel card = (CardModel)ModelDb.Get(cardType);

        Assert.Equal(expectedType, card.Type);
        Assert.Equal(expectedRarity, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.Equal(expectedCost, card.EnergyCost);
        Assert.Equal(expectedKeywords, card.Keywords);
        Assert.Equal(expectedModifierGeneration, card.CanBeGeneratedByModifiers);
        Assert.True(card.CanBeGeneratedInCombat);
    }

    [Theory]
    [MemberData(nameof(DamageCards))]
    public async Task PlayerTurnEnd_UsesExactDamageAndValueProps(
        Type cardType,
        int damage,
        ValueProp expectedProps,
        bool bypassesBlock)
    {
        (Player player, ICombatState combatState) = CreateContext();
        TurnEndDamageProbe probe = AddProbe(player);
        CardModel card = AddCardToHand(player, cardType);
        player.Creature.GainBlockInternal(20m);
        int hpBefore = player.Creature.CurrentHp;

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, combatState.Allies);
        await CombatEngine.DoTurnEndAsync(combatState, player);

        Assert.Equal(hpBefore - (bypassesBlock ? damage : 0), player.Creature.CurrentHp);
        Assert.Equal(bypassesBlock ? 20m : 20m - damage, player.Creature.Block);
        Assert.Equal(expectedProps, probe.LastProps);
        Assert.Same(card, probe.LastCardSource);
        Assert.Same(player.Creature, probe.LastDealer);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 0)]
    [InlineData(50, 40)]
    public async Task Debt_PlayerTurnEnd_LosesAtMostTenGold(int startingGold, int expectedGold)
    {
        (Player player, ICombatState combatState) = CreateContext();
        await PlayerCmd.LoseGold(player.Gold, player);
        await PlayerCmd.GainGold(startingGold, player);
        AddCardToHand(player, typeof(Debt));

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, combatState.Allies);
        await CombatEngine.DoTurnEndAsync(combatState, player);

        Assert.Equal(expectedGold, player.Gold);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Regret_SnapshotsInitialHandBeforeAnyRetainedEffect_RegardlessOfListenerOrder(
        bool mutatorBeforeRegret)
    {
        (Player player, ICombatState combatState) = CreateContext();
        TurnEndDamageProbe probe = AddProbe(player);
        var regret = (Regret)ModelDb.Card<Regret>().MutableClone();
        regret.AssignOwner(player);
        var mutator = (HandMutationOnTurnEndCard)new HandMutationOnTurnEndCard().MutableClone();
        mutator.AssignOwner(player);
        var filler = (Injury)ModelDb.Card<Injury>().MutableClone();
        filler.AssignOwner(player);
        mutator.CardToMove = filler;
        if (mutatorBeforeRegret)
        {
            player.PlayerCombatState!.Hand.AddInternal(mutator);
            player.PlayerCombatState.Hand.AddInternal(regret);
        }
        else
        {
            player.PlayerCombatState!.Hand.AddInternal(regret);
            player.PlayerCombatState.Hand.AddInternal(mutator);
        }
        player.PlayerCombatState!.Hand.AddInternal(filler);
        player.Creature.GainBlockInternal(20m);
        int hpBefore = player.Creature.CurrentHp;

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, combatState.Allies);
        await CombatEngine.DoTurnEndAsync(combatState, player);

        Assert.Equal(hpBefore - 3, player.Creature.CurrentHp);
        Assert.Equal(20m, player.Creature.Block);
        Assert.Same(player.PlayerCombatState.DiscardPile, filler.Pile);
        Assert.Equal(ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, probe.LastProps);
        Assert.Same(regret, probe.LastCardSource);
    }

    [Theory]
    [InlineData(typeof(Beckon), PileType.Discard)]
    [InlineData(typeof(Toxic), PileType.Exhaust)]
    public async Task PlayableStatus_HasNoOnPlayEffect_AndMovesToCanonicalPile(
        Type cardType,
        PileType expectedPile)
    {
        (Player player, _) = CreateContext();
        CardModel card = AddCardToHand(player, cardType);
        player.PlayerCombatState!.Energy = 3;
        int hpBefore = player.Creature.CurrentHp;

        bool canPlay = card.CanPlay(out UnplayableReason reason);
        await card.PlayAsync(null);

        Assert.True(canPlay);
        Assert.Equal(UnplayableReason.None, reason);
        Assert.Equal(hpBefore, player.Creature.CurrentHp);
        Assert.Equal(expectedPile, card.Pile!.Type);
        Assert.Equal(2, player.PlayerCombatState.Energy);
    }

    [Fact]
    public void Wither_CannotUseNormalUpgradeSystem()
    {
        var wither = (Wither)ModelDb.Card<Wither>().MutableClone();

        Assert.False(wither.IsUpgradable);
        Assert.Throws<InvalidOperationException>(wither.Upgrade);
    }

    // 原版 CombatManager.DoTurnEnd / DoTurnEndCards 与阶段二弃手牌（#32）：先按手牌顺序消耗虚无牌，
    // 再逐张结算回合末效果牌并放到弃牌堆底（虚无的则消耗），最后才弃剩下的手牌。
    [Theory]
    [InlineData("regret-in-middle")]
    [InlineData("ethereal-and-turn-end")]
    [InlineData("ethereal-turn-end-card")]
    public async Task EndPlayerTurn_OrdersTurnEndAndEtherealCardsLikeNative(string scenario)
    {
        var runState = new RunState("pile-order-" + scenario, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new Sts2Sim.Core.Rooms.CombatRoom(
            () => (MonsterModel)ModelDb.Monster<Sts2Sim.Core.Models.Monsters.WanderingGrunt>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        PlayerCombatState state = player.PlayerCombatState!;
        // 起手牌挪回抽牌堆，保证下个玩家回合抽牌时不会重洗弃牌堆。
        foreach (CardModel card in state.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Draw);
        }

        int discardBefore = state.DiscardPile.Cards.Count;
        int exhaustBefore = state.ExhaustPile.Cards.Count;
        CardModel defend = AddCardToHand(player, typeof(DefendRegent));
        CardModel[] expectedDiscard;
        CardModel[] expectedExhaust;
        EtherealTurnEndProbeCard? probe = null;
        switch (scenario)
        {
            case "regret-in-middle":
            {
                CardModel regret = AddCardToHand(player, typeof(Regret));
                CardModel defend2 = AddCardToHand(player, typeof(DefendRegent));
                CardModel strike = AddCardToHand(player, typeof(StrikeRegent));
                expectedDiscard = [regret, defend, defend2, strike];
                expectedExhaust = [];
                break;
            }
            case "ethereal-and-turn-end":
            {
                CardModel bane = AddCardToHand(player, typeof(AscendersBane));
                CardModel regret = AddCardToHand(player, typeof(Regret));
                CardModel strike = AddCardToHand(player, typeof(StrikeRegent));
                expectedDiscard = [regret, defend, strike];
                expectedExhaust = [bane];
                break;
            }
            default:
            {
                probe = (EtherealTurnEndProbeCard)new EtherealTurnEndProbeCard().MutableClone();
                probe.AssignOwner(player);
                state.Hand.AddInternal(probe);
                CardModel strike = AddCardToHand(player, typeof(StrikeRegent));
                expectedDiscard = [defend, strike];
                expectedExhaust = [probe];
                break;
            }
        }

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(expectedDiscard, state.DiscardPile.Cards.Skip(discardBefore), ReferenceEqualityComparer.Instance);
        Assert.Equal(expectedExhaust, state.ExhaustPile.Cards.Skip(exhaustBefore), ReferenceEqualityComparer.Instance);
        if (probe is not null)
        {
            Assert.Equal(1, probe.TriggerCount);
        }
    }

    [Theory]
    [InlineData("regret-debt")]
    [InlineData("ethereal-effect")]
    [InlineData("lethal-effect")]
    [InlineData("paused-effect")]
    public async Task TurnEnd_PreservesPhasesAndEnding(string scenario)
    {
        var run = new RunState("399-turn-end-" + scenario, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var room = new Sts2Sim.Core.Rooms.CombatRoom(
            () => (MonsterModel)ModelDb.Monster<Sts2Sim.Core.Models.Monsters.WanderingGrunt>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        PlayerCombatState piles = player.PlayerCombatState!;
        foreach (CardModel card in piles.Hand.Cards.ToArray())
            CardPileCmd.Add(card, PileType.Draw);
        ICombatState combat = player.Creature.CombatState!;
        int playsBefore = piles.CardsPlayedThisCombat;

        if (scenario == "paused-effect")
        {
            var first = (PausedTurnEndProbeCard)new PausedTurnEndProbeCard().MutableClone();
            first.AssignOwner(player);
            piles.Hand.AddInternal(first);
            CardModel second = AddCardToHand(player, typeof(Debt));
            Task turn = CombatEngine.DoTurnEndAsync(combat, player);
            PileType? secondPileWhilePaused;
            try
            {
                await first.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                secondPileWhilePaused = second.Pile?.Type;
            }
            finally
            {
                first.Release.TrySetResult();
                await turn;
            }

            // Native DoTurnEndCards starts B's move independently of A's effect completion.
            // In Instant/NonInteractive mode Cmd.Wait is complete: B must already be in Play.
            Assert.True(secondPileWhilePaused == PileType.Play,
                $"seed=399-turn-end-{scenario}; native Instant dependency contract expects B in Play while A waits; actual={secondPileWhilePaused}; resumed effect saw hand={first.HandCountAfterPause}");
            Assert.Equal(0, first.HandCountAfterPause);
        }
        else if (scenario == "ethereal-effect")
        {
            var effect = (EtherealTurnEndProbeCard)new EtherealTurnEndProbeCard().MutableClone();
            effect.AssignOwner(player);
            piles.Hand.AddInternal(effect);
            await CombatEngine.DoTurnEndAsync(combat, player);
            Assert.Equal(1, effect.TriggerCount);
            Assert.Same(piles.ExhaustPile, effect.Pile);
        }
        else
        {
            CardModel regret = AddCardToHand(player, typeof(Regret));
            CardModel? debt = null;
            int hp = player.Creature.CurrentHp;
            int gold = player.Gold;
            if (scenario == "lethal-effect")
                player.Creature.SetCurrentHpInternal(1);
            else
                debt = AddCardToHand(player, typeof(Debt));
            await Hook.BeforeSideTurnEnd(combat, CombatSide.Player, combat.Allies);
            await CombatEngine.DoTurnEndAsync(combat, player);

            if (scenario == "lethal-effect")
            {
                Assert.Equal(0, player.Creature.CurrentHp);
                Assert.Same(piles.PlayPile, regret.Pile);
            }
            else
            {
                Assert.Equal(hp - 2, player.Creature.CurrentHp);
                Assert.Equal(Math.Max(0, gold - 10), player.Gold);
                Assert.Same(piles.DiscardPile, regret.Pile);
                Assert.Same(piles.DiscardPile, debt!.Pile);
                Assert.True(piles.DiscardPile.Cards.ToList().IndexOf(regret) < piles.DiscardPile.Cards.ToList().IndexOf(debt));
            }
        }
        Assert.Equal(playsBefore, piles.CardsPlayedThisCombat);
    }

    private static (Player Player, ICombatState CombatState) CreateContext()
    {
        var runState = new RunState("task7-turn-end-in-hand-cards", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        return (player, combatState);
    }

    private static TurnEndDamageProbe AddProbe(Player player)
    {
        var probe = (TurnEndDamageProbe)new TurnEndDamageProbe().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        return probe;
    }

    private static CardModel AddCardToHand(Player player, Type cardType)
    {
        var card = (CardModel)((CardModel)ModelDb.Get(cardType)).MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }
}
