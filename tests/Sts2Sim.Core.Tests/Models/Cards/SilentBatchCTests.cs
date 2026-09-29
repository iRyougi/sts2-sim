namespace Sts2Sim.Core.Tests.Models.Cards;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class SilentBatchCTests : IDisposable
{
    private static readonly Type[] BatchTypes =
    [
        typeof(Silent), typeof(RingOfTheSnake), typeof(WanderingGrunt),
        typeof(StrikeSilent), typeof(DefendSilent), typeof(Neutralize), typeof(Survivor),
        typeof(Shiv), typeof(WeakPower), typeof(FanOfKnivesPower),
        typeof(Accuracy), typeof(Acrobatics), typeof(BladeOfInk), typeof(BladeDance),
        typeof(BladeSymphony), typeof(CalculatedGamble), typeof(CloakAndDagger),
        typeof(DaggerThrow), typeof(FanOfKnives), typeof(HiddenDaggers),
        typeof(InfiniteBlades), typeof(KnifeTrap), typeof(LeadingStrike),
        typeof(MementoMori), typeof(PhantomBlades), typeof(Prepared), typeof(Reflex),
        typeof(ShadowStep), typeof(StormOfSteel), typeof(Tactician),
        typeof(ToolsOfTheTrade), typeof(UpMySleeve),
        typeof(AccuracyPower), typeof(DoubleDamagePower), typeof(InfiniteBladesPower),
        typeof(PhantomBladesPower), typeof(ShadowStepPower), typeof(ToolsOfTheTradePower),
        typeof(Inky),
    ];

    public SilentBatchCTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(BatchTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 10)]
    public async Task Accuracy_AddsOfficialDamageToShivs(bool upgraded, int expectedDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"accuracy-{upgraded}");
        Accuracy accuracy = AddToHand<Accuracy>(player, upgraded);
        await room.Engine.PlayCardAsync(player, accuracy, null);
        Shiv shiv = AddToHand<Shiv>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, shiv, enemy);

        Assert.Equal(expectedDamage, hp - enemy.CurrentHp);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public async Task Acrobatics_DrawsThenDiscardsOneSelectedCard(bool upgraded, int drawCount)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"acrobatics-{upgraded}");
        Acrobatics card = AddToHand<Acrobatics>(player, upgraded);
        AddDrawCards(player, drawCount);
        CardModel selected = player.PlayerCombatState!.DrawPile.Cards.Last();
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(selected);

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(drawCount - 1, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Contains(selected, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task BladeOfInk_GeneratesInkyShivsWithBaseDamageAndWeak(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"blade-ink-{upgraded}");
        await room.Engine.PlayCardAsync(player, AddToHand<BladeOfInk>(player, upgraded), null);
        Assert.Equal(count, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
        Shiv shiv = player.PlayerCombatState.Hand.Cards.OfType<Shiv>().First();
        Assert.IsType<Inky>(Assert.Single(shiv.Enchantments));
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, shiv, enemy);

        Assert.Equal(4, hp - enemy.CurrentHp);
        Assert.Equal(1, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public async Task BladeDance_GeneratesOfficialShivCountAndExhausts(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"blade-dance-{upgraded}");
        BladeDance card = AddToHand<BladeDance>(player, upgraded);
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Equal(count, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
        Assert.Contains(card, player.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task BladeSymphony_GivesTwoShivsToEachLivingTeammateOnly()
    {
        (IReadOnlyList<Player> players, CombatRoom room) = await CreateMultiplayerCombatAsync("blade-symphony", 3);
        BladeSymphony card = AddToHand<BladeSymphony>(players[0]);
        await room.Engine.PlayCardAsync(players[0], card, target: null);
        Assert.Empty(players[0].PlayerCombatState!.Hand.Cards.OfType<Shiv>());
        Assert.All(players.Skip(1), player => Assert.Equal(2, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count()));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CalculatedGamble_DiscardsWholeHandDrawsSameCountAndUpgradeRetains(bool upgraded, bool retain)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"gamble-{upgraded}");
        CalculatedGamble card = AddToHand<CalculatedGamble>(player, upgraded);
        StrikeSilent first = AddToHand<StrikeSilent>(player);
        StrikeSilent second = AddToHand<StrikeSilent>(player);
        AddDrawCards(player, 2);
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Contains(first, player.PlayerCombatState!.DiscardPile.Cards);
        Assert.Contains(second, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(retain, card.HasKeyword(CardKeyword.Retain));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task CloakAndDagger_GainsSixBlockAndGeneratesShivs(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"cloak-{upgraded}");
        await room.Engine.PlayCardAsync(player, AddToHand<CloakAndDagger>(player, upgraded), null);
        Assert.Equal(6, player.Creature.Block);
        Assert.Equal(count, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
    }

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public async Task DaggerThrow_DamagesDrawsThenDiscards(bool upgraded, int damage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"dagger-throw-{upgraded}");
        DaggerThrow card = AddToHand<DaggerThrow>(player, upgraded);
        AddDrawCards(player, 1);
        CardModel selected = player.PlayerCombatState!.DrawPile.Cards.Single();
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(selected);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(player, card, enemy);
        Assert.Equal(damage, hp - enemy.CurrentHp);
        Assert.Contains(selected, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public async Task FanOfKnives_AppliesMarkerAndGeneratesShivs(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"fan-{upgraded}");
        await room.Engine.PlayCardAsync(player, AddToHand<FanOfKnives>(player, upgraded), null);
        Assert.Single(player.Creature.Powers.OfType<FanOfKnivesPower>());
        Assert.Equal(count, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
        Assert.All(player.PlayerCombatState.Hand.Cards.OfType<Shiv>(), shiv => Assert.Equal(TargetType.AllEnemies, shiv.TargetType));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task HiddenDaggers_DiscardsTwoAndGeneratesTwoShivsWithUpgradeState(bool upgraded, bool generatedUpgraded)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"hidden-{upgraded}");
        HiddenDaggers card = AddToHand<HiddenDaggers>(player, upgraded);
        CardModel one = AddToHand<StrikeSilent>(player);
        CardModel two = AddToHand<StrikeSilent>(player);
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(one, two);
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Contains(one, player.PlayerCombatState!.DiscardPile.Cards);
        Assert.Contains(two, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.OfType<Shiv>().Count());
        Assert.All(player.PlayerCombatState.Hand.Cards.OfType<Shiv>(), shiv => Assert.Equal(generatedUpgraded, shiv.IsUpgraded));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task InfiniteBlades_GeneratesBeforeHandDrawAndUpgradeIsInnate(bool upgraded, bool innate)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"infinite-{upgraded}");
        InfiniteBlades card = AddToHand<InfiniteBlades>(player, upgraded);
        await room.Engine.PlayCardAsync(player, card, null);
        await Hook.BeforeHandDraw(room.Engine.State, player);
        Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<Shiv>());
        Assert.Equal(innate, card.HasKeyword(CardKeyword.Innate));
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 12)]
    public async Task KnifeTrap_AutoPlaysEveryExhaustShivWithoutEnergyAndPreservesExhaust(bool upgraded, int expectedDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"knife-trap-{upgraded}");
        KnifeTrap trap = AddToHand<KnifeTrap>(player, upgraded);
        Shiv first = AddToPile<Shiv>(player, PileType.Exhaust);
        Shiv second = AddToPile<Shiv>(player, PileType.Exhaust);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;
        player.PlayerCombatState!.Energy = trap.EnergyCost;
        await room.Engine.PlayCardAsync(player, trap, enemy);
        Assert.Equal(expectedDamage, hp - enemy.CurrentHp);
        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.Contains(first, player.PlayerCombatState.ExhaustPile.Cards);
        Assert.Contains(second, player.PlayerCombatState.ExhaustPile.Cards);
        Assert.All(new[] { first, second }, shiv => Assert.Equal(upgraded, shiv.IsUpgraded));
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 6)]
    public async Task LeadingStrike_DamagesAndGeneratesTwoShivs(bool upgraded, int damage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"leading-{upgraded}");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(player, AddToHand<LeadingStrike>(player, upgraded), enemy);
        Assert.Equal(damage, hp - enemy.CurrentHp);
        Assert.Equal(2, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
    }

    [Theory]
    [InlineData(false, false, 17)]
    [InlineData(true, false, 21)]
    [InlineData(false, true, 17)]
    public async Task MementoMori_ScalesFromCardsDiscardedThisTurn(
        bool upgraded, bool createAfterDiscard, int damage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"memento-{upgraded}-{createAfterDiscard}");
        MementoMori? card = createAfterDiscard ? null : AddToHand<MementoMori>(player, upgraded);
        await CardCmd.Discard(new[] { AddToHand<StrikeSilent>(player), AddToHand<StrikeSilent>(player) });
        card ??= AddToHand<MementoMori>(player, upgraded);
        Assert.Equal(2, room.Engine.State.Clone().Players[0].PlayerCombatState!.CardsDiscardedThisTurn);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hp = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(player, card, enemy);
        Assert.Equal(damage, hp - enemy.CurrentHp);
    }

    [Theory]
    [InlineData(false, false, 13)]
    [InlineData(true, false, 16)]
    [InlineData(false, true, 4)]
    [InlineData(true, true, 4)]
    public async Task PhantomBlades_RetainsShivsAndBuffsOnlyFirstShivEachTurn(bool upgraded, bool shivBeforePower, int firstDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"phantom-{upgraded}-{shivBeforePower}");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        Shiv[] shivs = [];
        if (shivBeforePower)
        {
            shivs = (await Shiv.CreateInHand(player, 3, room.Engine.State)).Cast<Shiv>().ToArray();
            await room.Engine.PlayCardAsync(player, shivs[0], enemy);
            Assert.Equal(1, room.Engine.State.Clone().Players[0].PlayerCombatState!.ShivsPlayedThisTurn);
        }

        await room.Engine.PlayCardAsync(player, AddToHand<PhantomBlades>(player, upgraded), null);
        if (!shivBeforePower)
        {
            shivs = (await Shiv.CreateInHand(player, 2, room.Engine.State)).Cast<Shiv>().ToArray();
        }
        Shiv first = shivs[shivBeforePower ? 1 : 0];
        Shiv second = shivs[shivBeforePower ? 2 : 1];
        Assert.True(first.HasKeyword(CardKeyword.Retain));
        int hp = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(player, first, enemy);
        int afterFirst = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(player, second, enemy);
        Assert.Equal(firstDamage, hp - afterFirst);
        Assert.Equal(4, afterFirst - enemy.CurrentHp);
        player.PlayerCombatState!.EndOfTurnCleanup();
        Assert.Equal(0, player.PlayerCombatState.ShivsPlayedThisTurn);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task Prepared_DrawsThenDiscardsOfficialCount(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"prepared-{upgraded}");
        Prepared card = AddToHand<Prepared>(player, upgraded);
        AddDrawCards(player, count);
        CardModel[] selected = player.PlayerCombatState!.DrawPile.Cards.ToArray();
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(selected);
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.All(selected, item => Assert.Contains(item, player.PlayerCombatState.DiscardPile.Cards));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task Reflex_IsSlyAndDrawsOfficialCount(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"reflex-{upgraded}");
        Reflex card = AddToHand<Reflex>(player, upgraded);
        AddDrawCards(player, count);
        player.PlayerCombatState!.Energy = 3;
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.True(card.HasKeyword(CardKeyword.Sly));
        Assert.Equal(count, player.PlayerCombatState.Hand.Cards.Count);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task ShadowStep_DiscardsHandThenGrantsNextTurnDoubleDamage(bool upgraded, int cost)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"shadow-step-{upgraded}");
        ShadowStep card = AddToHand<ShadowStep>(player, upgraded);
        CardModel discarded = AddToHand<StrikeSilent>(player);
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Equal(cost, card.EnergyCost);
        Assert.Contains(discarded, player.PlayerCombatState!.DiscardPile.Cards);
        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Player, new[] { player.Creature });
        Assert.Single(player.Creature.Powers.OfType<DoubleDamagePower>());
        Assert.Empty(player.Creature.Powers.OfType<ShadowStepPower>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task StormOfSteel_ReplacesHandWithSameCountShivsAndUpgradeState(bool upgraded, bool shivsUpgraded)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"storm-{upgraded}");
        StormOfSteel card = AddToHand<StormOfSteel>(player, upgraded);
        AddToHand<StrikeSilent>(player);
        AddToHand<StrikeSilent>(player);
        await room.Engine.PlayCardAsync(player, card, null);
        Shiv[] shivs = player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().ToArray();
        Assert.Equal(2, shivs.Length);
        Assert.All(shivs, shiv => Assert.Equal(shivsUpgraded, shiv.IsUpgraded));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task Tactician_IsSlyAndGainsEnergy(bool upgraded, int gain)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"tactician-{upgraded}");
        Tactician card = AddToHand<Tactician>(player, upgraded);
        player.PlayerCombatState!.Energy = 3;
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.True(card.HasKeyword(CardKeyword.Sly));
        Assert.Equal(gain, player.PlayerCombatState.Energy);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task ToolsOfTheTrade_AddsOneDrawThenDiscardsOneAtTurnStart(bool upgraded, int cost)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"tools-{upgraded}");
        ToolsOfTheTrade card = AddToHand<ToolsOfTheTrade>(player, upgraded);
        await room.Engine.PlayCardAsync(player, card, null);
        CardModel selected = AddToHand<StrikeSilent>(player);
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(selected);
        Assert.Equal(8, Hook.ModifyHandDraw(room.Engine.State, player, 5));
        await Hook.AfterPlayerTurnStart(room.Engine.State, player);
        Assert.Equal(cost, card.EnergyCost);
        Assert.Contains(selected, player.PlayerCombatState!.DiscardPile.Cards);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public async Task UpMySleeve_GeneratesShivsAndReducesCostEachPlayThisCombat(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"sleeve-{upgraded}");
        UpMySleeve card = AddToHand<UpMySleeve>(player, upgraded);
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Equal(count, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
        Assert.Equal(1, card.EnergyCost);
    }

    [Fact]
    public async Task RunDecisionSource_DefaultCardSelectionMatchesRunEnginePolicy()
    {
        (Player player, _) = await CreateCombatAsync("run-selection-default");
        CardModel[] candidates = Enumerable.Range(0, 4)
            .Select(_ => (CardModel)ModelDb.Card<StrikeSilent>().MutableClone())
            .ToArray();
        IRunDecisionSource source = new MinimalRunDecisionSource();

        IReadOnlyList<CardModel> selected = await ((ICardSelectionDecisionSource)source)
            .ChooseCardsAsync(new CardSelectionRequest(player, candidates, 2, 2, null));

        Assert.Equal(2, selected.Count);
        Assert.Same(candidates[2], selected[0]);
        Assert.Same(candidates[3], selected[1]);
    }

    private static TCard AddToHand<TCard>(Player player, bool upgraded = false) where TCard : CardModel =>
        AddToPile<TCard>(player, PileType.Hand, upgraded);

    private static TCard AddToPile<TCard>(Player player, PileType pile, bool upgraded = false) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        if (upgraded) card.Upgrade();
        CardPileCmd.Add(card, pile);
        return card;
    }

    private static void AddDrawCards(Player player, int count)
    {
        for (int i = 0; i < count; i++) AddToPile<StrikeSilent>(player, PileType.Draw);
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        (IReadOnlyList<Player> players, CombatRoom room) = await CreateMultiplayerCombatAsync(seed, 1);
        return (players[0], room);
    }

    private static async Task<(IReadOnlyList<Player> Players, CombatRoom Room)> CreateMultiplayerCombatAsync(string seed, int playerCount)
    {
        var runState = new RunState(seed, new Overgrowth());
        var players = new List<Player>();
        for (int i = 0; i < playerCount; i++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
            runState.AddPlayer(player);
            players.Add(player);
        }
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        foreach (Player player in players)
        {
            foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
                foreach (CardModel card in pile.Cards.ToArray()) CardPileCmd.Remove(card);
            player.PlayerCombatState.Energy = 10;
        }
        return (players, room);
    }

    private sealed class MinimalRunDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options[0]);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    private sealed class FixedSelectionSource(params CardModel[] selected) : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Assert.All(selected, card => Assert.Contains(card, request.Candidates));
            Assert.InRange(selected.Length, request.MinCount, request.MaxCount);
            return Task.FromResult<IReadOnlyList<CardModel>>(selected);
        }
    }
}
