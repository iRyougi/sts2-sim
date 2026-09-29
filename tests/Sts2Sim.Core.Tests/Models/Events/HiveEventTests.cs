using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using LostWispEvent = Sts2Sim.Core.Models.Events.LostWisp;
using PaelsLegion = Sts2Sim.Core.Models.Relics.PaelsLegion;

namespace Sts2Sim.Core.Tests.Models.Events;

file sealed class HiveAncientVetoRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShouldAllowAncient(IRunState runState, Player player, AncientEventModel ancientEvent) => false;
}

file sealed class LanternKeyCombatDecisionSource : IRunDecisionSource
{
    public MysteriousKnight? Knight { get; private set; }
    public decimal KnightHpAtFirstDecision { get; private set; }
    public int StrengthAtFirstDecision { get; private set; }
    public int PlatingAtFirstDecision { get; private set; }
    public string? MoveAtFirstDecision { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        if (Knight is null)
        {
            Knight = state.Enemies.Single().Monster as MysteriousKnight
                ?? throw new InvalidOperationException("Expected the precreated MysteriousKnight.");
            KnightHpAtFirstDecision = Knight.Creature.CurrentHp;
            StrengthAtFirstDecision = Knight.Creature.GetPower<StrengthPower>()?.Amount ?? 0;
            PlatingAtFirstDecision = Knight.Creature.GetPower<PlatingPower>()?.Amount ?? 0;
            MoveAtFirstDecision = Knight.NextMove?.StateId;
        }

        Player player = state.Players[0];
        CardModel? attack = player.PlayerCombatState!.Hand.Cards.FirstOrDefault(
            card => card.Type == CardType.Attack &&
                    card.TargetType == TargetType.AnyEnemy &&
                    card.CanPlay(out _));
        return Task.FromResult<CombatDecision>(attack is null
            ? new CombatDecision.EndTurn()
            : new CombatDecision.PlayCard(attack, state.HittableEnemies[0]));
    }
}

[Collection("ModelDb")]
public sealed class HiveEventTests : IDisposable
{
    private static readonly Type[] ExpectedPool =
    {
        typeof(Amalgamator), typeof(Bugslayer), typeof(ColorfulPhilosophers), typeof(ColossalFlower),
        typeof(FieldOfManSizedHoles), typeof(InfestedAutomaton), typeof(LostWispEvent),
        typeof(SpiritGrafter), typeof(TheLanternKey), typeof(ZenWeaver),
    };

    public HiveEventTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(HiveAncientVetoRelic)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Act2EventPool_ContainsExactlyTheTenCanonicalEvents()
    {
        Assert.Equal(ExpectedPool, Act2EventPool.All);
        Assert.Equal(10, Act2EventPool.All.Distinct().Count());
    }

    [Fact]
    public async Task Amalgamator_GatesOnTwoBasicStrikesAndDefends_AndCombinesEitherPair()
    {
        (RunState run, Player player) = CreateRun("amalgamator-strikes");
        var ev = Begin<Amalgamator>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(new[] { "COMBINE_STRIKES", "COMBINE_DEFENDS" }, ev.CurrentOptions.Select(x => x.Key));

        await Choose(ev, "COMBINE_STRIKES");
        Assert.Equal(2, player.Deck.Cards.Count(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike)));
        Assert.Single(player.Deck.Cards.OfType<UltimateStrike>());
        Assert.True(ev.IsFinished);

        (run, player) = CreateRun("amalgamator-defends");
        ev = Begin<Amalgamator>(run, player);
        await Choose(ev, "COMBINE_DEFENDS");
        Assert.Equal(2, player.Deck.Cards.Count(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Defend)));
        Assert.Single(player.Deck.Cards.OfType<UltimateDefend>());

        foreach (CardModel card in player.Deck.Cards.Where(card => card.Tags.Contains(CardTag.Strike)).Skip(1).ToList())
        {
            await CardPileCmd.RemoveFromDeck(player, card);
        }
        Assert.False(ev.IsAllowed(run));
    }

    [Theory]
    [InlineData("EXTERMINATION", typeof(Exterminate))]
    [InlineData("SQUASH", typeof(Squash))]
    public async Task Bugslayer_HasTwoOptions_AndAddsTheChosenEventCard(string key, Type expectedType)
    {
        (RunState run, Player player) = CreateRun($"bugslayer-{key}");
        var ev = Begin<Bugslayer>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(2, ev.CurrentOptions.Count);
        int count = player.Deck.Cards.Count;

        await Choose(ev, key);

        Assert.Equal(count + 1, player.Deck.Cards.Count);
        Assert.Equal(expectedType, player.Deck.Cards[^1].GetType());
        Assert.True(ev.IsFinished);
        Assert.Equal(0, ev.Rng.Counter);
    }

    [Fact]
    public async Task ColorfulPhilosophers_OffersOtherUnlockedColor_AndThreeRarityRewards()
    {
        (RunState run, Player player) = CreateRun("colorful");
        var ev = Begin<ColorfulPhilosophers>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(new[] { "IRONCLAD", "SILENT", "DEFECT" },
            ev.CurrentOptions.Select(candidate => candidate.Key));
        EventOption option = Assert.Single(ev.CurrentOptions, candidate => candidate.Key == "SILENT");
        Assert.Equal(0, ev.Rng.Counter);

        int rewardsBefore = player.PlayerRng.Rewards.Counter;
        await ev.ChooseOption(option);

        var offered = new List<RewardsSet>();
        while (ev.TryDequeuePendingRewardOffer(out RewardsSet? rewards))
        {
            offered.Add(rewards);
        }
        Assert.Equal(3, offered.Count);
        Assert.Equal(new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare },
            offered.Select(set => Assert.Single(set.Card.Options.Select(card => card.Rarity).Distinct())));
        Assert.All(offered, set => Assert.Equal(3, set.Card.Options.Count));
        Assert.Equal(18, player.PlayerRng.Rewards.Counter - rewardsBefore);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task ColossalFlower_GatesAtNineteenHp_AndImplementsEveryDigPrize()
    {
        (RunState run, Player player) = CreateRun("flower-gold");
        var ev = Begin<ColossalFlower>(run, player);
        player.Creature.LoseHpInternal(player.Creature.CurrentHp - 18, default);
        Assert.False(ev.IsAllowed(run));
        await CreatureCmd.Heal(player.Creature, 1m);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(new[] { "EXTRACT_CURRENT_PRIZE", "REACH_DEEPER" }, ev.CurrentOptions.Select(x => x.Key));
        int gold = player.Gold;
        await Choose(ev, "EXTRACT_CURRENT_PRIZE");
        Assert.Equal(gold + 35, player.Gold);

        (run, player) = CreateRun("flower-middle-gold");
        ev = Begin<ColossalFlower>(run, player);
        await Choose(ev, "REACH_DEEPER");
        int hp = player.Creature.CurrentHp;
        gold = player.Gold;
        await Choose(ev, "EXTRACT_CURRENT_PRIZE");
        Assert.Equal(hp, player.Creature.CurrentHp);
        Assert.Equal(gold + 75, player.Gold);

        (run, player) = CreateRun("flower-core");
        ev = Begin<ColossalFlower>(run, player);
        hp = player.Creature.CurrentHp;
        await Choose(ev, "REACH_DEEPER");
        Assert.Equal(hp - 5, player.Creature.CurrentHp);
        Assert.Equal(new[] { "EXTRACT_CURRENT_PRIZE", "REACH_DEEPER" }, ev.CurrentOptions.Select(x => x.Key));
        await Choose(ev, "REACH_DEEPER");
        Assert.Equal(hp - 11, player.Creature.CurrentHp);
        Assert.Equal(new[] { "EXTRACT_INSTEAD", "POLLINOUS_CORE" }, ev.CurrentOptions.Select(x => x.Key));
        await Choose(ev, "POLLINOUS_CORE");
        Assert.Equal(hp - 18, player.Creature.CurrentHp);
        Assert.Single(player.Relics.OfType<PollinousCore>());

        (run, player) = CreateRun("flower-final-gold");
        ev = Begin<ColossalFlower>(run, player);
        await Choose(ev, "REACH_DEEPER");
        await Choose(ev, "REACH_DEEPER");
        hp = player.Creature.CurrentHp;
        gold = player.Gold;
        await Choose(ev, "EXTRACT_INSTEAD");
        Assert.Equal(hp, player.Creature.CurrentHp);
        Assert.Equal(gold + 135, player.Gold);
    }

    [Fact]
    public async Task FieldOfManSizedHoles_GatesOnEnchantableCard_AndImplementsBothOptions()
    {
        (RunState run, Player player) = CreateRun("holes-resist");
        var ev = Begin<FieldOfManSizedHoles>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(new[] { "RESIST", "ENTER_YOUR_HOLE" }, ev.CurrentOptions.Select(x => x.Key));
        int count = player.Deck.Cards.Count;
        await Choose(ev, "RESIST");
        Assert.Equal(count - 1, player.Deck.Cards.Count);
        Assert.Single(player.Deck.Cards.OfType<Normality>());

        (run, player) = CreateRun("holes-enchant");
        ev = Begin<FieldOfManSizedHoles>(run, player);
        await Choose(ev, "ENTER_YOUR_HOLE");
        Assert.Single(player.Deck.Cards.SelectMany(card => card.Enchantments).OfType<PerfectFit>());

        foreach (CardModel card in player.Deck.Cards.ToList())
        {
            CardPileCmd.Remove(card);
        }
        Assert.False(ev.IsAllowed(run));
    }

    [Theory]
    [InlineData("STUDY")]
    [InlineData("TOUCH_CORE")]
    public async Task InfestedAutomaton_UsesRewardRngAndAddsAValidCard(string key)
    {
        (RunState run, Player player) = CreateRun($"automaton-{key}");
        var ev = Begin<InfestedAutomaton>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(2, ev.CurrentOptions.Count);
        int rngBefore = player.PlayerRng.Rewards.Counter;
        int count = player.Deck.Cards.Count;

        await Choose(ev, key);

        CardModel added = player.Deck.Cards[^1];
        Assert.Equal(count + 1, player.Deck.Cards.Count);
        if (key == "STUDY")
        {
            Assert.Equal(CardType.Power, added.Type);
        }
        else
        {
            Assert.Equal(0, added.EnergyCost);
            Assert.False(added.CostsXEnergy);
        }
        Assert.Equal(2, player.PlayerRng.Rewards.Counter - rngBefore);
        Assert.Equal(0, ev.Rng.Counter);
    }

    [Fact]
    public async Task LostWisp_ConsumesOneEventRoll_AndImplementsSearchAndClaim()
    {
        (RunState run, Player player) = CreateRun("lost-wisp-search");
        var ev = Begin<LostWispEvent>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(new[] { "CLAIM", "SEARCH" }, ev.CurrentOptions.Select(x => x.Key));
        Assert.Equal(1, ev.Rng.Counter);
        int gold = player.Gold;
        await Choose(ev, "SEARCH");
        Assert.InRange(player.Gold - gold, 45, 75);
        Assert.Equal(1, ev.Rng.Counter);

        (run, player) = CreateRun("lost-wisp-claim");
        ev = Begin<LostWispEvent>(run, player);
        await Choose(ev, "CLAIM");
        Assert.Single(player.Deck.Cards.OfType<Decay>());
        Assert.Single(player.Relics.OfType<Sts2Sim.Core.Models.Relics.LostWisp>());
    }

    [Fact]
    public async Task SpiritGrafter_HealsAndAddsMetamorphosis_OrUpgradesThenDamages()
    {
        (RunState run, Player player) = CreateRun("grafter-heal");
        run.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        player.Creature.LoseHpInternal(player.Creature.CurrentHp - 1, default);
        var ev = Begin<SpiritGrafter>(run, player);
        Assert.Equal(new[] { "LET_IT_IN", "REJECTION" }, ev.CurrentOptions.Select(x => x.Key));
        await Choose(ev, "LET_IT_IN");
        Assert.Equal(26, player.Creature.CurrentHp);
        Assert.Single(player.Deck.Cards.OfType<Metamorphosis>());

        (run, player) = CreateRun("grafter-reject");
        run.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        ev = Begin<SpiritGrafter>(run, player);
        CardModel first = player.Deck.Cards.First(card => card.IsUpgradable);
        int hp = player.Creature.CurrentHp;
        await Choose(ev, "REJECTION");
        Assert.True(first.IsUpgraded);
        Assert.Equal(hp - 10, player.Creature.CurrentHp);
        Assert.Equal(0, ev.Rng.Counter);
    }

    [Theory]
    [InlineData("return")]
    [InlineData("fight-win")]
    [InlineData("fight-knight-state")]
    public async Task TheLanternKey_OptionsMatchNativeFlow(string scenario)
    {
        var run = new RunState($"lantern-{scenario}", new Overgrowth(), ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);

        var eventRoom = new EventRoom(
            () => (EventModel)ModelDb.Event<TheLanternKey>().MutableClone());
        run.PushRoom(eventRoom);
        int nicheBefore = run.Rng.Niche.Counter;
        await eventRoom.Enter(run);
        var ev = Assert.IsType<TheLanternKey>(eventRoom.Event);

        Assert.True(ev.IsAllowed(run));
        Assert.Equal(new[] { "RETURN_THE_KEY", "KEEP_THE_KEY" }, ev.CurrentOptions.Select(x => x.Key));
        Assert.Equal(1, run.Rng.Niche.Counter - nicheBefore);

        if (scenario == "return")
        {
            int gold = player.Gold;
            await Choose(ev, "RETURN_THE_KEY");
            Assert.Equal(gold + 100, player.Gold);
            Assert.True(ev.IsFinished);
            Assert.False(ev.HasPendingForcedCombat);
            Assert.Empty(player.Deck.Cards.OfType<LanternKey>());
            return;
        }

        await CreatureCmd.SetMaxAndCurrentHp(player.Creature, 1_000_000_000m);
        await Choose(ev, "KEEP_THE_KEY");
        Assert.Equal("FIGHT", Assert.Single(ev.CurrentOptions).Key);

        if (scenario == "fight-knight-state")
        {
            var decisions = new LanternKeyCombatDecisionSource();
            await new RunDriver(run, decisions).DriveEventAsync(eventRoom);

            MysteriousKnight knight = Assert.IsType<MysteriousKnight>(decisions.Knight);
            Assert.Equal(108m, knight.Creature.MaxHp);
            Assert.Equal(108m, decisions.KnightHpAtFirstDecision);
            Assert.Equal(6, decisions.StrengthAtFirstDecision);
            Assert.Equal(6, decisions.PlatingAtFirstDecision);
            Assert.Equal("RAM_MOVE", decisions.MoveAtFirstDecision);
            Assert.Equal(
                new[] { "FLAIL_MOVE", "RAM_MOVE", "RAND", "WAR_CHANT" },
                knight.MoveStateMachine!.States.Keys.OrderBy(key => key));
        }
        else
        {
            await Choose(ev, "FIGHT");
            Assert.True(ev.IsAwaitingForcedCombat);
            Assert.True(ev.HasPendingForcedCombat);
            Assert.Empty(player.Deck.Cards.OfType<LanternKey>());

            await new RunEngine(run, points => points[0]).DriveEventAsync(eventRoom);
        }

        Assert.Equal(1, run.Rng.Niche.Counter - nicheBefore);
        Assert.True(ev.IsFinished);
        Assert.False(ev.HasPendingForcedCombat);
        Assert.Single(player.Deck.Cards.OfType<LanternKey>());
    }

    [Theory]
    [MemberData(nameof(DeterministicNormalEventTypes))]
    public async Task DeterministicNormalEvents_DoNotConsumeEventRng(Type eventType)
    {
        (RunState run, Player player) = CreateRun($"no-event-rng-{eventType.Name}");
        player.Gold = 250;
        var ev = (EventModel)ModelDb.Get(eventType).MutableClone();
        ev.AssignOwner(player);

        ev.BeginEvent(run);
        Assert.True(ev.IsAllowed(run));
        await ev.ChooseOption(ev.CurrentOptions[0]);

        Assert.Equal(0, ev.Rng.Counter);
    }

    [Theory]
    [InlineData("BREATHING_TECHNIQUES", 50, 2)]
    [InlineData("EMOTIONAL_AWARENESS", 125, -1)]
    [InlineData("ARACHNID_ACUPUNCTURE", 250, -2)]
    public async Task ZenWeaver_GatesAt125Gold_AndImplementsEachPurchase(string key, int cost, int deckDelta)
    {
        (RunState run, Player player) = CreateRun($"zen-{key}");
        player.Gold = 250;
        var ev = Begin<ZenWeaver>(run, player);
        Assert.True(ev.IsAllowed(run));
        Assert.Equal(3, ev.CurrentOptions.Count);
        int count = player.Deck.Cards.Count;

        await Choose(ev, key);

        Assert.Equal(250 - cost, player.Gold);
        Assert.Equal(count + deckDelta, player.Deck.Cards.Count);
        if (key == "BREATHING_TECHNIQUES")
        {
            Assert.Equal(2, player.Deck.Cards.OfType<Enlightenment>().Count());
        }
        Assert.True(ev.IsFinished);

        (run, player) = CreateRun($"zen-gate-{key}");
        player.Gold = 124;
        Assert.False(ModelDb.Event<ZenWeaver>().IsAllowed(run));
    }

    [Theory]
    [InlineData(typeof(Orobas), 5, 13)]
    [InlineData(typeof(Pael), 3, 10)]
    [InlineData(typeof(Tezcatara), 3, 10)]
    public async Task HiveAncients_HealOfferThreeRngChoicesAndObtainAncientRelic(
        Type ancientType, int expectedRngCalls, int expectedAllOptions)
    {
        (RunState run, Player player) = CreateRun($"ancient-{ancientType.Name}");
        player.Creature.LoseHpInternal(30, default);
        var ancient = (AncientEventModel)ModelDb.Get(ancientType).MutableClone();
        ancient.AssignOwner(player);

        ancient.BeginEvent(run);

        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
        Assert.Equal(3, ancient.CurrentOptions.Count);
        Assert.Equal(expectedRngCalls, ancient.Rng.Counter);
        Assert.Equal(expectedAllOptions, ancient.AllPossibleOptions.Count);
        string[] generatedKeys = ancient.CurrentOptions.Select(x => x.Key).ToArray();
        EventOption chosen = ancient.CurrentOptions[0];
        await ancient.ChooseOption(chosen);
        Assert.True(ancient.IsFinished);
        Assert.Equal(RelicRarity.Ancient, player.Relics[^1].Rarity);

        (RunState sameRun, Player samePlayer) = CreateRun($"ancient-{ancientType.Name}");
        var sameAncient = (AncientEventModel)ModelDb.Get(ancientType).MutableClone();
        sameAncient.AssignOwner(samePlayer);
        sameAncient.BeginEvent(sameRun);
        Assert.Equal(generatedKeys, sameAncient.CurrentOptions.Select(x => x.Key));
    }

    [Theory]
    [InlineData(typeof(Orobas))]
    [InlineData(typeof(Pael))]
    [InlineData(typeof(Tezcatara))]
    public async Task HiveAncients_EachGeneratedOptionObtainsItsNamedRelic(Type ancientType)
    {
        for (int optionIndex = 0; optionIndex < 3; optionIndex++)
        {
            (RunState run, Player player) = CreateRun($"ancient-option-{ancientType.Name}-{optionIndex}");
            var ancient = (AncientEventModel)ModelDb.Get(ancientType).MutableClone();
            ancient.AssignOwner(player);
            ancient.BeginEvent(run);
            EventOption option = ancient.CurrentOptions[optionIndex];

            await ancient.ChooseOption(option);

            Assert.Equal(option.Key, player.Relics[^1].GetType().Name);
            Assert.Equal(RelicRarity.Ancient, player.Relics[^1].Rarity);
            Assert.True(ancient.IsFinished);
        }
    }
    [Fact]
    public void HiveAncients_RespectSourcePoolEligibilityGates()
    {
        (RunState run, Player player) = CreateRun("orobas-locked-pool");
        foreach (RelicModel relic in player.Relics.ToList())
        {
            player.RemoveRelicInternal(relic);
        }
        foreach (CardModel card in player.Deck.Cards.ToList())
        {
            CardPileCmd.Remove(card);
        }
        var orobas = Begin<Orobas>(run, player);
        Assert.Equal("OPTION_POOL_3_LOCKED", orobas.CurrentOptions[2].Key);
        Assert.Equal(4, orobas.Rng.Counter);

        (run, player) = CreateRun("pael-gated-pools");
        foreach (CardModel card in player.Deck.Cards.ToList())
        {
            CardPileCmd.Remove(card);
        }
        var pet = (PaelsLegion)ModelDb.Relic<PaelsLegion>().MutableClone();
        pet.AssignOwner(player);
        player.AddRelicInternal(pet);
        var pael = Begin<Pael>(run, player);
        Assert.Contains(pael.CurrentOptions[1].Key, new[] { nameof(PaelsWing), nameof(PaelsGrowth) });
        Assert.Contains(pael.CurrentOptions[2].Key, new[] { nameof(PaelsEye), nameof(PaelsBlood) });

        (run, player) = CreateRun("tezcatara-no-basic-strike");
        foreach (CardModel card in player.Deck.Cards.Where(card =>
                     card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike)).ToList())
        {
            CardPileCmd.Remove(card);
        }
        var tezcatara = Begin<Tezcatara>(run, player);
        Assert.NotEqual(nameof(NutritiousSoup), tezcatara.CurrentOptions[0].Key);
    }

    [Theory]
    [InlineData(typeof(Orobas))]
    [InlineData(typeof(Pael))]
    [InlineData(typeof(Tezcatara))]
    public async Task HiveAncients_UseAncientVetoProceedSemantics(Type ancientType)
    {
        (RunState run, Player player) = CreateRun($"ancient-veto-{ancientType.Name}");
        var veto = (HiveAncientVetoRelic)ModelDb.Relic<HiveAncientVetoRelic>().MutableClone();
        veto.AssignOwner(player);
        player.AddRelicInternal(veto);
        var ancient = (AncientEventModel)ModelDb.Get(ancientType).MutableClone();
        ancient.AssignOwner(player);

        ancient.BeginEvent(run);

        EventOption proceed = Assert.Single(ancient.CurrentOptions);
        Assert.Equal("PROCEED", proceed.Key);
        Assert.Equal(0, ancient.Rng.Counter);
        await ancient.ChooseOption(proceed);
        Assert.True(ancient.IsFinished);
    }

    [Fact]
    public async Task Orobas_LockedOptionCannotBeChosen()
    {
        (RunState run, Player player) = CreateRun("orobas-locked-choice");
        foreach (RelicModel relic in player.Relics.ToList()) player.RemoveRelicInternal(relic);
        foreach (CardModel card in player.Deck.Cards.ToList()) CardPileCmd.Remove(card);
        var orobas = Begin<Orobas>(run, player);
        EventOption locked = orobas.CurrentOptions[2];
        Assert.True(locked.IsLocked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => orobas.ChooseOption(locked));
        Assert.False(orobas.IsFinished);
    }

    [Fact]
    public void Ancient_WearyTravelerHealsEightyPercentOfMissingHp()
    {
        var run = new RunState("weary-hive", new Overgrowth(),
            (int)Sts2Sim.Core.Entities.Ascension.AscensionLevel.WearyTraveler);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        player.Creature.LoseHpInternal(25m, default);
        _ = Begin<Orobas>(run, player);
        Assert.Equal(player.Creature.MaxHp - 5, player.Creature.CurrentHp);
    }

    [Theory]
    [InlineData(125, false)]
    [InlineData(249, false)]
    [InlineData(250, true)]
    public void ZenWeaver_ThirdOptionUsesExactGoldBoundary(int gold, bool enabled)
    {
        (RunState run, Player player) = CreateRun($"zen-boundary-{gold}");
        player.Gold = gold;
        var ev = Begin<ZenWeaver>(run, player);
        Assert.Equal(enabled, ev.CurrentOptions[2].IsEnabled);
    }

    [Fact]
    public void Goopy_OnlyAcceptsDefendTaggedCards()
    {
        (RunState run, Player player) = CreateRun("goopy-gate");
        Goopy goopy = ModelDb.GetById<Goopy>(ModelDb.GetId<Goopy>());
        Assert.All(player.Deck.Cards.Where(card => card.Tags.Contains(CardTag.Defend)), card => Assert.True(goopy.CanEnchant(card)));
        Assert.All(player.Deck.Cards.Where(card => card.Tags.Contains(CardTag.Strike)), card => Assert.False(goopy.CanEnchant(card)));
        foreach (CardModel defend in player.Deck.Cards.Where(card => card.Tags.Contains(CardTag.Defend)).ToList())
            CardPileCmd.Remove(defend);
        var pael = Begin<Pael>(run, player);
        Assert.DoesNotContain(nameof(PaelsClaw), pael.CurrentOptions.Select(option => option.Key));
    }
    [Fact]
    public async Task ColorfulPhilosophers_Act2UpgradeRollsUseRewardsStream()
    {
        int upgraded = 0;
        for (int seed = 0; seed < 20; seed++)
        {
            var run = new RunState($"colorful-act2-{seed}", new ActDefinition[] { new Overgrowth(), new HiveTestAct2() });
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
            run.AddPlayer(player);
            run.AdvanceToNextAct();
            var ev = Begin<ColorfulPhilosophers>(run, player);
            int before = player.PlayerRng.Rewards.Counter;
            Assert.Equal(new[] { "IRONCLAD", "SILENT", "DEFECT" },
                ev.CurrentOptions.Select(candidate => candidate.Key));
            await ev.ChooseOption(Assert.Single(ev.CurrentOptions, candidate => candidate.Key == "SILENT"));
            Assert.Equal(18, player.PlayerRng.Rewards.Counter - before);
            while (ev.TryDequeuePendingRewardOffer(out RewardsSet? rewards))
                upgraded += rewards.Card.Options.Count(card => card.IsUpgraded);
        }
        Assert.True(upgraded > 0);
    }

    [Theory]
    [InlineData("STUDY")]
    [InlineData("TOUCH_CORE")]
    public async Task InfestedAutomaton_DefaultOddsReachEveryRarityWithTwoRolls(string key)
    {
        var rarities = new HashSet<CardRarity>();
        for (int seed = 0; seed < 150; seed++)
        {
            (RunState run, Player player) = CreateRun($"automaton-rarity-{key}-{seed}");
            var ev = Begin<InfestedAutomaton>(run, player);
            int before = player.PlayerRng.Rewards.Counter;
            await Choose(ev, key);
            Assert.Equal(2, player.PlayerRng.Rewards.Counter - before);
            rarities.Add(player.Deck.Cards[^1].Rarity);
        }
        CardRarity[] expected = key == "STUDY"
            ? [CardRarity.Uncommon, CardRarity.Rare]
            : [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];
        Assert.Equal(expected, rarities.Order());
    }
    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static TEvent Begin<TEvent>(RunState runState, Player player)
        where TEvent : EventModel
    {
        var ev = (TEvent)ModelDb.Event<TEvent>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return ev;
    }

    private static Task Choose(EventModel ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));

    public static TheoryData<Type> DeterministicNormalEventTypes => new()
    {
        typeof(Amalgamator),
        typeof(Bugslayer),
        typeof(ColorfulPhilosophers),
        typeof(ColossalFlower),
        typeof(FieldOfManSizedHoles),
        typeof(InfestedAutomaton),
        typeof(SpiritGrafter),
        typeof(TheLanternKey),
        typeof(ZenWeaver),
    };
}
