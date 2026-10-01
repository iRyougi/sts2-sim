using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Runs.Transplant;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class AncientRelicBatch1Tests : IDisposable
{
    public AncientRelicBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(CursedPearlOrderProbe))
                .Append(typeof(PrecariousShearsOrderProbe)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void FixedRarityFactory_ReturnsEveryEligibleCanonicalOnceAndAdvancesOnlyForSuccessfulPicks()
    {
        (_, Player player) = CreateRun("fixed-rarity-full-pool");
        CardModel[] eligible = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, isMultiplayer: false)
            .Where(card => !card.IsColorless && card.Rarity == CardRarity.Rare)
            .ToArray();
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        IReadOnlyList<CardModel> options = CardFactory.CreateForReward(
            player,
            eligible.Length + 2,
            CardRarity.Rare);

        Assert.Equal(eligible.Length, options.Count);
        Assert.Equal(eligible.Length, options.Select(card => card.Id).Distinct().Count());
        Assert.All(options, card =>
        {
            Assert.True(card.IsCanonical);
            Assert.Contains(card, eligible);
        });
        Assert.Equal(rewardsCounterBefore + eligible.Length, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public void FixedRarityFactory_WithNoEligibleCards_ReturnsEmptyWithoutAdvancingRng()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
        });
        (_, Player player) = CreateRun("fixed-rarity-empty-pool");
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        IReadOnlyList<CardModel> options = CardFactory.CreateForReward(player, 3, CardRarity.Rare);

        Assert.Empty(options);
        Assert.Equal(rewardsCounterBefore, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public async Task ArcaneScroll_AddsOneOwnedSinglePlayerNonColorlessRareUsingOneRewardsRoll()
    {
        var generatedTypes = new HashSet<Type>();

        for (int i = 0; i < 32; i++)
        {
            (_, Player player) = CreateRun($"arcane-scroll-{i}");
            int deckCountBefore = player.Deck.Cards.Count;
            int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;
            await RelicCmd.Obtain(ModelDb.Relic<Glitter>(), player);

            await RelicCmd.Obtain(ModelDb.Relic<ArcaneScroll>(), player);

            CardModel added = Assert.Single(player.Deck.Cards.Skip(deckCountBefore));
            generatedTypes.Add(added.GetType());
            Assert.Equal(CardRarity.Rare, added.Rarity);
            Assert.False(added.IsColorless);
            Assert.False(added.IsMultiplayerOnly);
            Assert.Same(player, added.Owner);
            Assert.Same(player.Deck, added.Pile);
            Assert.False(added.IsCanonical);
            Assert.IsType<Glam>(Assert.Single(added.Enchantments));
            Assert.Equal(rewardsCounterBefore + 1, player.PlayerRng.Rewards.Counter);
        }

        Assert.True(generatedTypes.Count > 1);
    }

    [Fact]
    public async Task ArcaneScroll_WhenNoEligibleRareExists_FailsBeforeDeckMutation()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
            typeof(ArcaneScroll),
        });
        (_, Player player) = CreateRun("arcane-scroll-empty");
        CardModel[] deckBefore = player.Deck.Cards.ToArray();
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RelicCmd.Obtain(ModelDb.Relic<ArcaneScroll>(), player));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Equal(rewardsCounterBefore, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public void ArcaneScroll_IsAncientWithoutUponPickupPreviewFlag()
    {
        ArcaneScroll relic = ModelDb.Relic<ArcaneScroll>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task GoldenPearl_GainsExactlyOneHundredFiftyGold()
    {
        (_, Player player) = CreateRun("golden-pearl");
        int goldBefore = player.Gold;

        await RelicCmd.Obtain(ModelDb.Relic<GoldenPearl>(), player);

        Assert.Equal(goldBefore + 150, player.Gold);
        GoldenPearl relic = Assert.Single(player.Relics.OfType<GoldenPearl>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task NutritiousSoup_EnchantsEligibleBasicStrikesWithPermanentZeroCostEternalAndThreeDamage()
    {
        (_, Player player) = CreateRun("nutritious-soup-ember");
        CardModel[] strikes = player.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike))
            .ToArray();
        CardModel[] otherCards = player.Deck.Cards.Except(strikes).ToArray();
        Assert.NotEmpty(strikes);

        await RelicCmd.Obtain(ModelDb.Relic<NutritiousSoup>(), player);

        Assert.All(strikes, card =>
        {
            TezcatarasEmber ember = Assert.IsType<TezcatarasEmber>(Assert.Single(card.Enchantments));
            Assert.Equal(0, card.LocalEnergyCost);
            Assert.True(card.HasKeyword(CardKeyword.Eternal));
            Assert.Equal(3m, ember.EnchantDamageAdditive(0m, Sts2Sim.Core.ValueProps.ValueProp.Move));
            Assert.Equal(0m, ember.EnchantDamageAdditive(0m, Sts2Sim.Core.ValueProps.ValueProp.Unpowered));
        });
        Assert.All(otherCards, card => Assert.Empty(card.Enchantments));
    }

    [Fact]
    public async Task GoldenCompass_ReplacesOnlyCurrentActWithConnectedGoldenPathAndEventUnknowns()
    {
        var run = new RunState("golden-compass-path", [new Overgrowth(), new Hive()]);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        ActMap original = run.Map;

        await RelicCmd.Obtain(ModelDb.Relic<GoldenCompass>(), player);

        GoldenCompass compass = Assert.Single(player.Relics.OfType<GoldenCompass>());
        Assert.Equal(0, compass.GoldenPathAct);
        Assert.NotSame(original, run.Map);
        MapPointType[] expected =
        [
            MapPointType.Monster, MapPointType.Unknown, MapPointType.Monster,
            MapPointType.RestSite, MapPointType.Monster, MapPointType.RestSite,
            MapPointType.Unknown, MapPointType.Treasure, MapPointType.Unknown,
            MapPointType.Treasure, MapPointType.Unknown, MapPointType.Shop,
            MapPointType.Elite, MapPointType.RestSite, MapPointType.Elite,
            MapPointType.RestSite,
        ];
        Assert.Equal(expected, Enumerable.Range(1, expected.Length)
            .Select(row => Assert.Single(run.Map.GetPointsInRow(row)).PointType));
        Assert.Equal(MapPointType.Ancient, run.Map.StartingMapPoint.PointType);
        Assert.Equal(MapPointType.Boss, run.Map.BossMapPoint.PointType);
        MapPoint previous = run.Map.StartingMapPoint;
        foreach (int row in Enumerable.Range(1, expected.Length))
        {
            MapPoint next = Assert.Single(run.Map.GetPointsInRow(row));
            Assert.Contains(next, previous.Children);
            previous = next;
        }
        Assert.Contains(run.Map.BossMapPoint, previous.Children);
        Assert.Equal([RoomType.Event], Hook.ModifyUnknownMapPointRoomTypes(run,
            new HashSet<RoomType> { RoomType.Monster, RoomType.Event }));

        _ = run.CurrentAncientEventType;
        MapPoint target = Assert.Single(run.Map.GetPointsInRow(1));
        RunTransplantSnapshot snapshot = RunTransplantExporter.Export(run, target);
        RunState imported = RunTransplantImporter.Import(snapshot);
        Assert.IsType<GoldenPathActMap>(imported.Map);
        Assert.Equal(expected, Enumerable.Range(1, expected.Length)
            .Select(row => Assert.Single(imported.Map.GetPointsInRow(row)).PointType));

        run.AdvanceToNextAct();
        Assert.IsType<StandardActMap>(run.Map);
        Assert.Equal(2, Hook.ModifyUnknownMapPointRoomTypes(run,
            new HashSet<RoomType> { RoomType.Monster, RoomType.Event }).Count);
    }

    [Fact]
    public async Task ToyBox_OffersFiveWaxRelicsAndMeltsTheFirstUnmeltedEveryThirdCombat()
    {
        (RunState run, Player player) = CreateRun("toy-box-five-wax");
        var room = new EventRoom(() => (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);

        await RelicCmd.Obtain(ModelDb.Relic<ToyBox>(), player);

        ToyBox box = Assert.Single(player.Relics.OfType<ToyBox>());
        Assert.True(box.HasUponPickupEffect);
        Assert.True(room.Event.TryDequeuePendingRewardOffer(out RewardsSet? offer));
        RelicReward[] rewards = Assert.IsType<RewardsSet>(offer).ExtraRewards
            .Select(Assert.IsType<RelicReward>).ToArray();
        Assert.Equal(5, rewards.Length);
        Assert.Equal(5, rewards.Select(reward => reward.Relic!.Id).Distinct().Count());
        foreach (RelicReward reward in rewards)
            await reward.Take();
        RelicModel[] wax = player.Relics.Where(relic => relic.IsWax).ToArray();
        Assert.Equal(5, wax.Length);
        Assert.All(wax, relic => Assert.False(relic.IsMelted));

        Assert.Same(room, run.PopCurrentRoom());
        var combatRoom = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        run.PushRoom(combatRoom);
        await combatRoom.Enter(run);
        await CreatureCmd.Damage(combatRoom.Engine.State,
            combatRoom.Engine.State.Enemies.ToArray(), 9999m, ValueProp.Unpowered,
            player.Creature, null, null);
        Assert.True(combatRoom.Engine.CheckWinCondition());
        await combatRoom.ResolveOutcomeAsync(generateRewards: false);
        Assert.Equal(1, box.CombatsSeen);
        await combatRoom.Exit(run);
        Assert.Same(combatRoom, run.PopCurrentRoom());

        for (int combat = 2; combat <= 15; combat++)
        {
            await box.AfterCombatEnd();
            Assert.Equal(combat, box.CombatsSeen);
            Assert.Equal(combat / 3, wax.Count(relic => relic.IsMelted));
            Assert.All(wax.Take(combat / 3), relic => Assert.True(relic.IsMelted));
            Assert.Equal(combat == 15, box.IsUsedUp);
        }
        Assert.All(wax, relic => Assert.Contains(relic, player.Relics));
    }

    [Fact]
    public async Task NewLeaf_TransformsFirstEligibleDeckCardUsingNicheRng()
    {
        (RunState runState, Player player) = CreateRun("new-leaf");
        CardModel[] deckBefore = player.Deck.Cards.ToArray();
        CardModel original = deckBefore.First(card => card.IsTransformable);
        int deckCountBefore = player.Deck.Cards.Count;
        int nicheCounterBefore = runState.Rng.Niche.Counter;
        int rewardsCounterBefore = player.PlayerRng.Rewards.Counter;
        int transformationsCounterBefore = player.PlayerRng.Transformations.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<NewLeaf>(), player);

        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, original));
        CardModel replacement = Assert.Single(player.Deck.Cards, card => !deckBefore.Contains(card));

        Assert.Equal(deckCountBefore, player.Deck.Cards.Count);
        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Same(player, replacement.Owner);
        Assert.Same(player.Deck, replacement.Pile);
        Assert.Equal(nicheCounterBefore + 1, runState.Rng.Niche.Counter);
        Assert.Equal(rewardsCounterBefore, player.PlayerRng.Rewards.Counter);
        Assert.Equal(transformationsCounterBefore, player.PlayerRng.Transformations.Counter);
        NewLeaf relic = Assert.Single(player.Relics.OfType<NewLeaf>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task NewLeaf_WhenNoTransformableCardExists_LeavesDeckAndNicheRngUntouched()
    {
        (RunState runState, Player player) = CreateRun("new-leaf-empty");
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
        CardModel greed = Assert.Single(await CardPileCmd.AddCursesToDeck(
            new[] { ModelDb.Card<Greed>() },
            player));
        int nicheCounterBefore = runState.Rng.Niche.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<NewLeaf>(), player);

        Assert.Same(greed, Assert.Single(player.Deck.Cards));
        Assert.Equal(nicheCounterBefore, runState.Rng.Niche.Counter);
    }

    [Fact]
    public async Task PreciseScissors_RemovesChosenRemovableDeckCard()
    {
        (RunState runState, Player player) = CreateRun("precise-scissors");
        CardModel selected = player.Deck.Cards.First(card => card.IsRemovable);
        CardModel[] untouched = player.Deck.Cards.Skip(1).ToArray();
        runState.ConfigureCardSelectionSource(new SelectedCardSource(selected));

        await RelicCmd.Obtain(ModelDb.Relic<PreciseScissors>(), player);

        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, selected));
        Assert.Null(selected.Pile);
        Assert.Equal(untouched, player.Deck.Cards);
        PreciseScissors relic = Assert.Single(player.Relics.OfType<PreciseScissors>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task PreciseScissors_WhenNoRemovableCardExists_LeavesDeckUntouched()
    {
        (_, Player player) = CreateRun("precise-scissors-empty");
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
        CardModel greed = Assert.Single(await CardPileCmd.AddCursesToDeck(
            new[] { ModelDb.Card<Greed>() },
            player));

        await RelicCmd.Obtain(ModelDb.Relic<PreciseScissors>(), player);

        Assert.Same(greed, Assert.Single(player.Deck.Cards));
    }

    [Fact]
    public async Task NutritiousOyster_GainsAndHealsExactlyElevenMaxHp()
    {
        (_, Player player) = CreateRun("nutritious-oyster");
        player.Creature.LoseHpInternal(20m, Sts2Sim.Core.ValueProps.ValueProp.Unpowered);
        int maxHpBefore = player.Creature.MaxHp;
        int currentHpBefore = player.Creature.CurrentHp;

        await RelicCmd.Obtain(ModelDb.Relic<NutritiousOyster>(), player);

        Assert.Equal(maxHpBefore + 11, player.Creature.MaxHp);
        Assert.Equal(currentHpBefore + 11, player.Creature.CurrentHp);
        NutritiousOyster relic = Assert.Single(player.Relics.OfType<NutritiousOyster>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task Pomander_UpgradesOnlyFirstUpgradableDeckCard()
    {
        (_, Player player) = CreateRun("pomander");
        CardModel selected = player.Deck.Cards[0];
        Assert.True(selected.IsUpgradable);

        await RelicCmd.Obtain(ModelDb.Relic<Pomander>(), player);

        Assert.True(selected.IsUpgraded);
        Assert.All(player.Deck.Cards.Skip(1), card => Assert.False(card.IsUpgraded));
        Pomander relic = Assert.Single(player.Relics.OfType<Pomander>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task Pomander_WhenNoUpgradableCardExists_LeavesDeckUntouched()
    {
        (_, Player player) = CreateRun("pomander-empty");
        foreach (CardModel card in player.Deck.Cards)
        {
            while (card.IsUpgradable)
            {
                card.Upgrade();
            }
        }
        CardModel[] deckBefore = player.Deck.Cards.ToArray();
        int[] levelsBefore = deckBefore.Select(card => card.CurrentUpgradeLevel).ToArray();

        await RelicCmd.Obtain(ModelDb.Relic<Pomander>(), player);

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Equal(levelsBefore, player.Deck.Cards.Select(card => card.CurrentUpgradeLevel));
    }

    [Fact]
    public async Task CursedPearl_AddsOwnedGreedBeforeGainingExactlyThreeHundredThirtyThreeGold()
    {
        (_, Player player) = CreateRun("cursed-pearl");
        await RelicCmd.Obtain(ModelDb.Relic<CursedPearlOrderProbe>(), player);
        var probe = Assert.Single(player.Relics.OfType<CursedPearlOrderProbe>());
        int goldBefore = player.Gold;
        int deckCountBefore = player.Deck.Cards.Count;

        await RelicCmd.Obtain(ModelDb.Relic<CursedPearl>(), player);

        Greed greed = Assert.Single(player.Deck.Cards.OfType<Greed>());
        Assert.Equal(deckCountBefore + 1, player.Deck.Cards.Count);
        Assert.Same(player, greed.Owner);
        Assert.Same(player.Deck, greed.Pile);
        Assert.False(greed.IsCanonical);
        Assert.Equal(goldBefore, probe.GoldWhenGreedEnteredDeck);
        Assert.Equal(goldBefore + 333, player.Gold);
        CursedPearl relic = Assert.Single(player.Relics.OfType<CursedPearl>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task PrecariousShears_RemovesFirstTwoBeforeTakingBlockableUnpoweredSixteenDamage()
    {
        (_, Player player) = CreateRun("precarious-shears");
        await RelicCmd.Obtain(ModelDb.Relic<PrecariousShearsOrderProbe>(), player);
        var probe = Assert.Single(player.Relics.OfType<PrecariousShearsOrderProbe>());
        CardModel[] removed = player.Deck.Cards.Take(2).ToArray();
        CardModel[] remaining = player.Deck.Cards.Skip(2).ToArray();
        player.Creature.GainBlockInternal(10m);
        int hpBefore = player.Creature.CurrentHp;

        await RelicCmd.Obtain(ModelDb.Relic<PrecariousShears>(), player);

        Assert.All(removed, card =>
        {
            Assert.DoesNotContain(player.Deck.Cards, candidate => ReferenceEquals(candidate, card));
            Assert.Null(card.Pile);
        });
        Assert.Equal(remaining, player.Deck.Cards);
        Assert.Equal(remaining, probe.DeckCardsAtDamage);
        Assert.Equal(16m, probe.ObservedAmount);
        Assert.Equal(Sts2Sim.Core.ValueProps.ValueProp.Unpowered, probe.ObservedProps);
        Assert.Equal(0, player.Creature.Block);
        Assert.Equal(hpBefore - 6, player.Creature.CurrentHp);
        PrecariousShears relic = Assert.Single(player.Relics.OfType<PrecariousShears>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task PrecariousShears_WithNoRemovableCards_StillTakesSixteenDamage()
    {
        (_, Player player) = CreateRun("precarious-shears-empty");
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
        CardModel greed = Assert.Single(await CardPileCmd.AddCursesToDeck(
            new[] { ModelDb.Card<Greed>() },
            player));
        int hpBefore = player.Creature.CurrentHp;

        await RelicCmd.Obtain(ModelDb.Relic<PrecariousShears>(), player);

        Assert.Same(greed, Assert.Single(player.Deck.Cards));
        Assert.Equal(hpBefore - 16, player.Creature.CurrentHp);
    }

    private sealed class SelectedCardSource(CardModel selected) : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>([selected]);
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        return (runState, player);
    }
}
