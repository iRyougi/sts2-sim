using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

public sealed class GloryEventTests : IDisposable
{
    public GloryEventTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Act3EventPool_ContainsExactlyTheSevenCanonicalEvents()
    {
        Assert.Equal(
            new[]
            {
                typeof(BattlewornDummy),
                typeof(GraveOfTheForgotten),
                typeof(HungryForMushrooms),
                typeof(Reflections),
                typeof(RoundTeaParty),
                typeof(Trial),
                typeof(TinkerTime),
            },
            Act3EventPool.All);
        Assert.Equal(7, Act3EventPool.All.Distinct().Count());
    }

    [Theory]
    [InlineData("SETTING_1", typeof(BattleFriendV1))]
    [InlineData("SETTING_2", typeof(BattleFriendV2))]
    [InlineData("SETTING_3", typeof(BattleFriendV3))]
    public async Task BattlewornDummy_EachSettingQueuesItsEnemyBattleFriendAndSuspends(
        string optionKey,
        Type expectedMonsterType)
    {
        (RunState runState, Player player, BattlewornDummy ev) = Begin<BattlewornDummy>($"battleworn-{optionKey}");

        Assert.Equal(new[] { "SETTING_1", "SETTING_2", "SETTING_3" }, ev.CurrentOptions.Select(option => option.Key));

        await Choose(ev, optionKey);

        Assert.False(ev.IsFinished);
        Assert.True(ev.IsAwaitingForcedCombat);
        Assert.True(ev.HasPendingForcedCombat);
        Func<IReadOnlyList<MonsterModel>> encounter = ev.DequeuePendingForcedCombatBatch();
        MonsterModel enemy = Assert.Single(encounter());
        Assert.IsType(expectedMonsterType, enemy);
    }

    [Theory]
    [InlineData("SETTING_1")]
    [InlineData("SETTING_2")]
    [InlineData("SETTING_3")]
    public async Task BattlewornDummy_VictoryBeforeTimeout_GrantsTheSelectedTierReward(string optionKey)
    {
        string seed = $"battleworn-victory-{optionKey}";
        (_, Player player, BattlewornDummy ev) = Begin<BattlewornDummy>(seed);

        await Choose(ev, optionKey);
        if (optionKey == "SETTING_1")
        {
            var probe = player.PlayerRng.Rewards.CloneExact();
            PotionModel expected = probe.NextItem(PotionFactory.GetOutOfCombatPool(player))!;
            int counterBefore = player.PlayerRng.Rewards.Counter;

            ev.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: false));

            Assert.True(ev.IsFinished);
            PotionModel potion = Assert.IsType<PotionReward>(Assert.Single(DrainRewardOffers(ev)).Potion).Potion!;
            Assert.Equal(expected.Id, potion.Id);
            Assert.Equal(probe.Counter - counterBefore, player.PlayerRng.Rewards.Counter - counterBefore);
            return;
        }

        if (optionKey == "SETTING_2")
        {
            List<CardModel> expected = player.Deck.Cards.Where(card => card.IsUpgradable).ToList();
            var probe = ev.Rng.CloneExact();
            expected.StableShuffle(probe);
            int counterBefore = ev.Rng.Counter;

            ev.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: false));

            Assert.True(ev.IsFinished);
            Assert.Equal(2, player.Deck.Cards.Count(card => card.IsUpgraded));
            Assert.All(expected.Take(2), card => Assert.True(card.IsUpgraded));
            Assert.Equal(probe.Counter - counterBefore, ev.Rng.Counter - counterBefore);
            Assert.False(ev.TryDequeuePendingRewardOffer(out _));
            return;
        }

        (_, Player relicProbeOwner) = CreateRun(seed);
        RelicModel expectedRelic = RelicFactory.PullNextRelicFromFront(relicProbeOwner);
        var relicRngProbe = player.PlayerRng.Rewards.CloneExact();
        _ = RelicFactory.RollRarity(relicRngProbe);
        int relicCounterBefore = player.PlayerRng.Rewards.Counter;

        ev.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: false));

        Assert.True(ev.IsFinished);
        RelicModel relic = Assert.IsType<RelicReward>(Assert.Single(DrainRewardOffers(ev)).Relic).Relic!;
        Assert.Equal(expectedRelic.Id, relic.Id);
        Assert.Equal(relicRngProbe.Counter - relicCounterBefore, player.PlayerRng.Rewards.Counter - relicCounterBefore);
    }

    [Fact]
    public async Task BattlewornDummy_DriverResumesAndDrainsPotionReward()
    {
        (RunState runState, Player player) = CreateRun("battleworn-driver-drain");
        var strength = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        strength.ApplyInternal(player.Creature, 100m);
        var room = new EventRoom(() => (EventModel)ModelDb.Event<BattlewornDummy>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);

        var decisions = new BattlewornDriverDecisionSource();
        await new RunDriver(runState, decisions).DriveEventAsync(room);

        Assert.True(room.Event.IsFinished);
        Assert.False(room.Event.IsAwaitingForcedCombat);
        Assert.True(decisions.RewardDecisionCount >= 2);
        Assert.Single(player.PotionSlots, potion => potion is not null);
    }

    [Fact]
    public async Task BattlewornDummy_DriverTreatsTimerExpiryBeforeSameHookDemiseAsTimeout()
    {
        (RunState runState, _) = CreateRun("battleworn-demise-race");
        var room = new EventRoom(() => (EventModel)ModelDb.Event<BattlewornDummy>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);

        var decisions = new BattlewornDemiseRaceDecisionSource();
        await new RunDriver(runState, decisions).DriveEventAsync(room);

        Assert.True(room.Event.IsFinished);
        Assert.Equal(0, decisions.RewardDecisionCount);
    }
    [Fact]
    public async Task BattlewornDummy_TimedOutAfterCombatWasWon_GrantsNoReward()
    {
        (_, _, BattlewornDummy ev) = Begin<BattlewornDummy>("battleworn-timeout");

        await Choose(ev, "SETTING_3");
        ev.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: true));

        Assert.True(ev.IsFinished);
        Assert.False(ev.TryDequeuePendingRewardOffer(out _));
    }
    [Fact]
    public async Task GraveOfTheForgotten_ConfrontAddsDecayAndTurnsExhaustIntoSouls()
    {
        (RunState runState, Player player) = CreateRun("grave-confront");
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
        var ev = (GraveOfTheForgotten)ModelDb.Event<GraveOfTheForgotten>().MutableClone();
        ev.AssignOwner(player);
        Assert.False(ev.IsAllowed(runState));
        CardModel firstShiv = await AddCard<Shiv>(player);
        CardModel selectedShiv = await AddCard<Shiv>(player);
        runState.ConfigureCardSelectionSource(new LastCardsSelectionSource());
        Assert.True(ev.IsAllowed(runState));
        ev.BeginEvent(runState);

        await Choose(ev, "CONFRONT");

        Assert.Single(player.Deck.Cards.OfType<Decay>());
        Assert.Empty(firstShiv.Enchantments.OfType<SoulsPower>());
        Assert.Single(selectedShiv.Enchantments.OfType<SoulsPower>());
        Assert.False(selectedShiv.HasKeyword(CardKeyword.Exhaust));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task GraveOfTheForgotten_AcceptObtainsForgottenSoul()
    {
        (_, Player player, GraveOfTheForgotten ev) = Begin<GraveOfTheForgotten>("grave-accept");

        await Choose(ev, "ACCEPT");

        Assert.Single(player.Relics.OfType<ForgottenSoul>());
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task HungryForMushrooms_BigGrantsTwentyMaxHpAndRelic()
    {
        (_, Player player, HungryForMushrooms ev) = Begin<HungryForMushrooms>("mushroom-big");
        int maxHpBefore = player.Creature.MaxHp;

        await Choose(ev, "BIG_MUSHROOM");

        Assert.Equal(maxHpBefore + 20, player.Creature.MaxHp);
        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
        Assert.Single(player.Relics.OfType<BigMushroom>());
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task HungryForMushrooms_FragrantDealsFifteenAndUpgradesTwoCards()
    {
        (RunState runState, Player player, HungryForMushrooms ev) = Begin<HungryForMushrooms>("mushroom-fragrant");
        int hpBefore = player.Creature.CurrentHp;
        List<CardModel> candidates = player.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        var probe = runState.Rng.Niche.CloneExact();
        List<CardModel> expected = candidates.ToList().StableShuffle(probe).Take(2).ToList();
        int counterBefore = runState.Rng.Niche.Counter;

        await Choose(ev, "FRAGRANT_MUSHROOM");

        Assert.Equal(hpBefore - 15, player.Creature.CurrentHp);
        Assert.Equal(2, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.All(expected, card => Assert.True(card.IsUpgraded));
        Assert.Equal(probe.Counter - counterBefore, runState.Rng.Niche.Counter - counterBefore);
        Assert.Single(player.Relics.OfType<FragrantMushroom>());
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Reflections_TouchDowngradesTwoThenUpgradesFourFromTheResultingDeck()
    {
        (RunState runState, Player player) = CreateRun("reflections-touch");
        foreach (CardModel card in player.Deck.Cards.ToList())
        {
            CardPileCmd.Remove(card);
        }
        for (int i = 0; i < 6; i++)
        {
            CardModel card = await AddCard<StrikeSilent>(player);
            if (i < 2)
            {
                CardCmd.Upgrade(card);
            }
        }
        var ev = Begin<Reflections>(runState, player);

        await Choose(ev, "TOUCH_A_MIRROR");

        Assert.Equal(4, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Reflections_ShatterCopiesOnlyTheOriginalDeckThenAddsBadLuck()
    {
        (_, Player player, Reflections ev) = Begin<Reflections>("reflections-shatter");
        int originalDeckSize = player.Deck.Cards.Count;

        await Choose(ev, "SHATTER");

        Assert.Equal(originalDeckSize * 2 + 1, player.Deck.Cards.Count);
        Assert.Single(player.Deck.Cards.OfType<BadLuck>());
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task RoundTeaParty_RequiresTwelveHpAndEnjoyFullyHealsWithRoyalPoison()
    {
        (RunState runState, Player player) = CreateRun("tea-enjoy");
        var prototype = ModelDb.Event<RoundTeaParty>();
        player.Creature.LoseHpInternal(player.Creature.CurrentHp - 11, default);
        Assert.False(prototype.IsAllowed(runState));
        player.Creature.HealInternal(1);
        Assert.True(prototype.IsAllowed(runState));
        player.Creature.LoseHpInternal(5, default);
        RoundTeaParty ev = Begin<RoundTeaParty>(runState, player);

        await Choose(ev, "ENJOY_TEA");

        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
        Assert.Single(player.Relics.OfType<RoyalPoison>());
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task RoundTeaParty_PickFightContinuesWithElevenUnblockableDamageAndFrontRelic()
    {
        (_, Player player, RoundTeaParty ev) = Begin<RoundTeaParty>("tea-fight");
        player.Creature.GainBlockInternal(20m);
        int hpBefore = player.Creature.CurrentHp;
        int relicsBefore = player.Relics.Count;

        await Choose(ev, "PICK_FIGHT");
        Assert.Equal("CONTINUE_FIGHT", Assert.Single(ev.CurrentOptions).Key);
        await Choose(ev, "CONTINUE_FIGHT");

        Assert.Equal(hpBefore - 11, player.Creature.CurrentHp);
        Assert.Equal(20, player.Creature.Block);
        Assert.Equal(relicsBefore + 1, player.Relics.Count);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Trial_MerchantVerdictsMatchTheTwoAuthoritativeCostsAndRewards()
    {
        (_, Player guiltyPlayer, Trial guilty) = await FindTrialPage("MERCHANT");
        int relicsBefore = guiltyPlayer.Relics.Count;
        await Choose(guilty, "MERCHANT_GUILTY");
        Assert.Single(guiltyPlayer.Deck.Cards.OfType<Regret>());
        Assert.Equal(relicsBefore + 2, guiltyPlayer.Relics.Count);

        (RunState innocentRunState, Player innocentPlayer, Trial innocent) = await FindTrialPage("MERCHANT");
        var merchantSelection = new LastCardsSelectionSource();
        innocentRunState.ConfigureCardSelectionSource(merchantSelection);
        CardModel firstUpgradable = innocentPlayer.Deck.Cards.First(card => card.IsUpgradable);
        await Choose(innocent, "MERCHANT_INNOCENT");
        Assert.Single(innocentPlayer.Deck.Cards.OfType<Shame>());
        Assert.Equal(2, innocentPlayer.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.False(firstUpgradable.IsUpgraded);
        Assert.All(merchantSelection.Selected, card => Assert.True(card.IsUpgraded));
    }

    [Fact]
    public async Task Trial_NobleVerdictsHealTenOrTradeRegretForThreeHundredGold()
    {
        (_, Player guiltyPlayer, Trial guilty) = await FindTrialPage("NOBLE");
        guiltyPlayer.Creature.LoseHpInternal(20, default);
        int hpBefore = guiltyPlayer.Creature.CurrentHp;
        await Choose(guilty, "NOBLE_GUILTY");
        Assert.Equal(hpBefore + 10, guiltyPlayer.Creature.CurrentHp);

        (_, Player innocentPlayer, Trial innocent) = await FindTrialPage("NOBLE");
        int goldBefore = innocentPlayer.Gold;
        await Choose(innocent, "NOBLE_INNOCENT");
        Assert.Single(innocentPlayer.Deck.Cards.OfType<Regret>());
        Assert.Equal(goldBefore + 300, innocentPlayer.Gold);
    }

    [Fact]
    public async Task Trial_NondescriptVerdictsOfferTwoCardRewardsOrTransformTwoCards()
    {
        (_, Player guiltyPlayer, Trial guilty) = await FindTrialPage("NONDESCRIPT");
        await Choose(guilty, "NONDESCRIPT_GUILTY");
        Assert.Single(guiltyPlayer.Deck.Cards.OfType<Doubt>());
        var offers = new List<RewardsSet>();
        while (guilty.TryDequeuePendingRewardOffer(out RewardsSet? reward))
        {
            offers.Add(reward);
        }
        Assert.Equal(2, offers.Count);
        Assert.All(offers, reward => Assert.Equal(3, reward.Card.Options.Count));

        (RunState innocentRunState, Player innocentPlayer, Trial innocent) = await FindTrialPage("NONDESCRIPT");
        var transformSelection = new NonFirstCardsSelectionSource();
        innocentRunState.ConfigureCardSelectionSource(transformSelection);
        CardModel firstTransformable = innocentPlayer.Deck.Cards.First(card => card.IsTransformable);
        int deckSizeBefore = innocentPlayer.Deck.Cards.Count;
        await Choose(innocent, "NONDESCRIPT_INNOCENT");
        Assert.Single(innocentPlayer.Deck.Cards.OfType<Doubt>());
        Assert.Equal(deckSizeBefore + 1, innocentPlayer.Deck.Cards.Count);
        Assert.Contains(firstTransformable, innocentPlayer.Deck.Cards);
        Assert.Equal(2, transformSelection.Selected.Count);
        Assert.All(
            transformSelection.Selected,
            original => Assert.DoesNotContain(
                innocentPlayer.Deck.Cards,
                current => ReferenceEquals(current, original)));
    }

    [Fact]
    public async Task Trial_RejectOffersRetryAndFatalDoubleDown()
    {
        (_, Player player, Trial ev) = Begin<Trial>("trial-reject");

        await Choose(ev, "REJECT");

        Assert.Equal(new[] { "ACCEPT", "DOUBLE_DOWN" }, ev.CurrentOptions.Select(option => option.Key));
        await Choose(ev, "DOUBLE_DOWN");
        Assert.True(player.Creature.IsDead);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Trial_DoubleDownCannotBePreventedByLizardTailOrFairyInABottle()
    {
        (_, Player player, Trial ev) = Begin<Trial>("trial-unpreventable-double-down");
        await RelicCmd.Obtain(ModelDb.Relic<LizardTail>(), player);
        var fairy = (FairyInABottle)ModelDb.Potion<FairyInABottle>().MutableClone();
        player.AddPotionInternal(fairy);

        await Choose(ev, "REJECT");
        await Choose(ev, "DOUBLE_DOWN");

        Assert.True(player.Creature.IsDead);
        Assert.False(Assert.Single(player.Relics.OfType<LizardTail>()).IsUsedUp);
        Assert.Contains(fairy, player.PotionSlots);
    }

    [Fact]
    public async Task TinkerTime_OffersTwoTypesThenTwoCompatibleRidersAndAddsMadScience()
    {
        (_, Player player, TinkerTime ev) = Begin<TinkerTime>("tinker");
        Assert.Equal("CHOOSE_CARD_TYPE", Assert.Single(ev.CurrentOptions).Key);

        await Choose(ev, "CHOOSE_CARD_TYPE");
        Assert.Equal(2, ev.CurrentOptions.Count);
        Assert.All(ev.CurrentOptions, option => Assert.Contains(option.Key, new[] { "ATTACK", "SKILL", "POWER" }));
        string typeKey = ev.CurrentOptions[0].Key;

        await Choose(ev, typeKey);
        Assert.Equal(2, ev.CurrentOptions.Count);
        string[] allowedRiders = typeKey switch
        {
            "ATTACK" => ["SAPPING", "VIOLENCE", "CHOKING"],
            "SKILL" => ["ENERGIZED", "WISDOM", "CHAOS"],
            _ => ["EXPERTISE", "CURIOUS", "IMPROVEMENT"],
        };
        Assert.All(ev.CurrentOptions, option => Assert.Contains(option.Key, allowedRiders));

        string riderKey = ev.CurrentOptions[0].Key;
        await Choose(ev, riderKey);

        MadScience card = Assert.Single(player.Deck.Cards.OfType<MadScience>());
        Assert.Equal(Enum.Parse<CardType>(typeKey, ignoreCase: true), card.Type);
        Assert.Equal(Enum.Parse<MadScienceRider>(riderKey, ignoreCase: true), card.Rider);
        Assert.Equal(CardRarity.Event, card.Rarity);
        Assert.Equal(1, card.EnergyCost);
        Assert.True(ev.IsFinished);

        CardCmd.Upgrade(card);
        Assert.True(card.HasKeyword(CardKeyword.Innate));
    }

    [Fact]
    public async Task ImprovementPower_UpgradesTwoDistinctCardsAfterCombat()
    {
        (RunState runState, Player player) = CreateRun("improvement");
        var power = (ImprovementPower)ModelDb.Power<ImprovementPower>().MutableClone();
        power.ApplyInternal(player.Creature, 2m);
        List<CardModel> candidates = player.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        var probe = runState.Rng.CombatCardSelection.CloneExact();
        var expected = new List<CardModel>();
        for (int i = 0; i < 2; i++)
        {
            CardModel selected = probe.NextItem(candidates)!;
            candidates.Remove(selected);
            expected.Add(selected);
        }
        int counterBefore = runState.Rng.CombatCardSelection.Counter;

        await power.AfterCombatEnd();

        Assert.Equal(2, player.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.All(expected, card => Assert.True(card.IsUpgraded));
        Assert.Equal(2, runState.Rng.CombatCardSelection.Counter - counterBefore);
    }

    [Fact]
    public void CuriousPower_ReducesOnlyTheOwnersPowerCardCost()
    {
        (_, Player player) = CreateRun("curious");
        var power = (CuriousPower)ModelDb.Power<CuriousPower>().MutableClone();
        power.ApplyInternal(player.Creature, 1m);
        CardModel ownedPower = (CardModel)ModelDb.Card<Afterimage>().MutableClone();
        ownedPower.AssignOwner(player);
        CardModel ownedAttack = (CardModel)ModelDb.Card<StrikeSilent>().MutableClone();
        ownedAttack.AssignOwner(player);

        Assert.True(power.TryModifyEnergyCostInCombat(ownedPower, 2m, out decimal reduced));
        Assert.Equal(1m, reduced);
        Assert.False(power.TryModifyEnergyCostInCombat(ownedAttack, 2m, out decimal unchanged));
        Assert.Equal(2m, unchanged);
    }

    [Theory]
    [InlineData(MadScienceRider.Sapping, 12, 2, 2, 0)]
    [InlineData(MadScienceRider.Violence, 36, 0, 0, 0)]
    [InlineData(MadScienceRider.Choking, 12, 0, 0, 6)]
    public async Task MadScience_AttackRidersMatchAuthoritativeEffects(
        MadScienceRider rider,
        int damage,
        int weak,
        int vulnerable,
        int strangle)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"mad-attack-{rider}");
        MadScience card = AddMadScience(player, CardType.Attack, rider);
        var enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, card, enemy);

        Assert.Equal(hpBefore - damage, enemy.CurrentHp);
        Assert.Equal(weak, enemy.Powers.OfType<WeakPower>().Sum(power => power.Amount));
        Assert.Equal(vulnerable, enemy.Powers.OfType<VulnerablePower>().Sum(power => power.Amount));
        Assert.Equal(strangle, enemy.Powers.OfType<StranglePower>().Sum(power => power.Amount));
    }

    [Theory]
    [InlineData(MadScienceRider.Energized)]
    [InlineData(MadScienceRider.Wisdom)]
    [InlineData(MadScienceRider.Chaos)]
    public async Task MadScience_SkillRidersGainEightBlockAndApplyTheirEffect(MadScienceRider rider)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"mad-skill-{rider}");
        if (rider == MadScienceRider.Wisdom)
        {
            for (int i = 0; i < 3; i++) AddCombatCard<StrikeSilent>(player, PileType.Draw);
        }
        MadScience card = AddMadScience(player, CardType.Skill, rider);

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(8, player.Creature.Block);
        if (rider == MadScienceRider.Energized)
        {
            Assert.Equal(100, player.PlayerCombatState!.Energy);
            Assert.Empty(player.Creature.Powers.OfType<EnergyNextTurnPower>());
        }
        else if (rider == MadScienceRider.Wisdom)
        {
            Assert.Equal(3, player.PlayerCombatState!.Hand.Cards.Count);
        }
        else
        {
            CardModel generated = Assert.Single(player.PlayerCombatState!.Hand.Cards);
            Assert.True(generated.TemporaryFreeThisTurn);
        }
    }

    [Fact]
    public async Task MadScience_ChaosUsesFullEligiblePoolShuffleAndMatchingRngCount()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("mad-skill-chaos-rng");
        MadScience card = AddMadScience(player, CardType.Skill, MadScienceRider.Chaos);
        var rng = room.Engine.State.RunState.Rng.CombatCardGeneration;
        var probe = rng.CloneExact();
        IReadOnlyList<CardModel> expected = CardFactory.GetDistinctForCombat(
            player,
            player.Character.CardPool.GetUnlockedCards(
                player.UnlockState,
                player.RunState.Players.Count > 1),
            1,
            probe);
        int counterBefore = rng.Counter;

        await room.Engine.PlayCardAsync(player, card, null);

        CardModel generated = Assert.Single(player.PlayerCombatState!.Hand.Cards);
        Assert.Equal(Assert.Single(expected).Id, generated.Id);
        Assert.Equal(probe.Counter - counterBefore, rng.Counter - counterBefore);
        Assert.True(generated.TemporaryFreeThisTurn);
    }

    [Theory]
    [InlineData(MadScienceRider.Expertise)]
    [InlineData(MadScienceRider.Curious)]
    [InlineData(MadScienceRider.Improvement)]
    public async Task MadScience_PowerRidersApplyTheirAuthoritativePowers(MadScienceRider rider)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"mad-power-{rider}");
        MadScience card = AddMadScience(player, CardType.Power, rider);

        await room.Engine.PlayCardAsync(player, card, null);

        if (rider == MadScienceRider.Expertise)
        {
            Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
            Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
        }
        else if (rider == MadScienceRider.Curious)
        {
            Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<CuriousPower>()).Amount);
        }
        else
        {
            Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<ImprovementPower>()).Amount);
        }
    }

    [Fact]
    public async Task ForgottenSoul_DealsOneUnpoweredDamageWhenItsOwnerExhausts()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("forgotten-soul-hook");
        await RelicCmd.Obtain(ModelDb.Relic<ForgottenSoul>(), player);
        ForgottenSoul relic = Assert.Single(player.Relics.OfType<ForgottenSoul>());
        CardModel exhausted = AddCombatCard<StrikeSilent>(player, PileType.Exhaust);
        var enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await relic.AfterCardExhausted(exhausted, causedByEthereal: false);

        Assert.Equal(hpBefore - 1, enemy.CurrentHp);
    }

    [Fact]
    public async Task RoyalPoison_HurtsItsOwnerOnlyOnTheFirstPlayerTurn()
    {
        (Player player, _) = await CreateCombatAsync("royal-poison-hook");
        await RelicCmd.Obtain(ModelDb.Relic<RoyalPoison>(), player);
        RoyalPoison relic = Assert.Single(player.Relics.OfType<RoyalPoison>());
        player.Creature.GainBlockInternal(10m);
        player.PlayerCombatState!.TurnNumber = 1;
        int hpBefore = player.Creature.CurrentHp;

        await relic.AfterPlayerTurnStart(player);

        Assert.Equal(hpBefore - 4, player.Creature.CurrentHp);
        Assert.Equal(10, player.Creature.Block);
        Assert.Null(Assert.Single(player.Creature.CombatState!.DamageHistory.Entries).Dealer);
        player.PlayerCombatState.TurnNumber = 2;
        await relic.AfterPlayerTurnStart(player);
        Assert.Equal(hpBefore - 4, player.Creature.CurrentHp);
    }

    private class BattlewornDriverDecisionSource : IRunDecisionSource
    {
        public int RewardDecisionCount { get; private set; }

        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            throw new InvalidOperationException("No map decision is expected.");

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
            Task.FromResult(options.Single(option => option.Key == "SETTING_1"));

        public virtual Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            Player player = state.Players[0];
            CardModel? attack = player.PlayerCombatState!.Hand.Cards
                .FirstOrDefault(card => card.Type == CardType.Attack && card.CanPlay(out _));
            return Task.FromResult<CombatDecision>(attack is null
                ? new CombatDecision.EndTurn()
                : new CombatDecision.PlayCard(attack, state.HittableEnemies[0]));
        }

        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
        {
            RewardDecisionCount++;
            return Task.FromResult(RewardDecisionClassifier.ChooseDefault(rewards));
        }
    }

    private sealed class BattlewornDemiseRaceDecisionSource : BattlewornDriverDecisionSource
    {
        private int _playerTurns;

        public override async Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            if (++_playerTurns == 3)
            {
                var enemy = Assert.Single(state.HittableEnemies);
                await PowerCmd.Apply<DemisePower>(state, enemy, enemy.CurrentHp, null, null);
            }

            return new CombatDecision.EndTurn();
        }
    }
    private static (RunState RunState, Player Player, TEvent Event) Begin<TEvent>(string seed)
        where TEvent : EventModel
    {
        (RunState runState, Player player) = CreateRun(seed);
        var ev = (TEvent)ModelDb.Event<TEvent>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static TEvent Begin<TEvent>(RunState runState, Player player)
        where TEvent : EventModel
    {
        var ev = (TEvent)ModelDb.Event<TEvent>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return ev;
    }

    private static IReadOnlyList<RewardsSet> DrainRewardOffers(EventModel ev)
    {
        var offers = new List<RewardsSet>();
        while (ev.TryDequeuePendingRewardOffer(out RewardsSet? rewards))
        {
            offers.Add(rewards);
        }
        return offers;
    }
    private static Task Choose(EventModel ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static int BattlewornRewardCount(Player player, string optionKey) => optionKey switch
    {
        "SETTING_1" => player.PotionSlots.Count(potion => potion is not null),
        "SETTING_2" => player.Deck.Cards.Count(card => card.IsUpgraded) / 2,
        _ => player.Relics.Count,
    };

    private static async Task<CardModel> AddCard<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (CardModel)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        await CardPileCmd.AddToDeck(card);
        return card;
    }

    private static MadScience AddMadScience(Player player, CardType type, MadScienceRider rider)
    {
        var card = (MadScience)ModelDb.Card<MadScience>().MutableClone();
        card.AssignOwner(player);
        card.Configure(type, rider);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static TCard AddCombatCard<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        (RunState runState, Player player) = CreateRun(seed);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToArray()) CardPileCmd.Remove(card);
        }
        player.PlayerCombatState.Energy = 99;
        return (player, room);
    }

    private static async Task<(RunState RunState, Player Player, Trial Event)> FindTrialPage(string pagePrefix)
    {
        for (int i = 0; i < 256; i++)
        {
            (RunState runState, Player player, Trial ev) = Begin<Trial>($"trial-{pagePrefix}-{i}");
            await Choose(ev, "ACCEPT");
            if (ev.CurrentOptions.All(option => option.Key.StartsWith(pagePrefix, StringComparison.Ordinal)))
            {
                return (runState, player, ev);
            }
        }

        throw new InvalidOperationException($"No deterministic seed produced Trial page {pagePrefix}.");
    }

    private sealed class LastCardsSelectionSource : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> Selected { get; private set; } = [];

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Selected = request.Candidates
                .Skip(request.Candidates.Count - request.MaxCount)
                .ToArray();
            return Task.FromResult(Selected);
        }
    }

    private sealed class NonFirstCardsSelectionSource : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> Selected { get; private set; } = [];

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Selected = request.Candidates.Skip(1).Take(request.MaxCount).ToArray();
            return Task.FromResult(Selected);
        }
    }
}
