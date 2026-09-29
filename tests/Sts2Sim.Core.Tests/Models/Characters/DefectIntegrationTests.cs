using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class DefectCharacterTests : IDisposable
{
    public DefectCharacterTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task StartingRun_MatchesSource()
    {
        Type[] sourceDeck =
        [
            typeof(StrikeDefect), typeof(StrikeDefect), typeof(StrikeDefect), typeof(StrikeDefect),
            typeof(DefendDefect), typeof(DefendDefect), typeof(DefendDefect), typeof(DefendDefect),
            typeof(Zap), typeof(Dualcast),
        ];
        var run = new RunState("defect-start-source", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Defect>(), run);
        run.AddPlayer(player);
        Assert.Equal(75, player.Creature.CurrentHp);
        Assert.Equal(75, player.Creature.MaxHp);
        Assert.Equal(99, player.Gold);
        Assert.Equal(3, player.MaxEnergy);
        Assert.Equal(3, player.BaseOrbSlotCount);
        Assert.Equal(sourceDeck, player.Deck.Cards.Select(card => card.GetType()));
        Assert.All(player.Deck.Cards, card => Assert.Same(player, card.Owner));
        Assert.IsType<CrackedCore>(Assert.Single(player.Relics));

        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        Assert.Equal(3, player.PlayerCombatState!.OrbQueue.Capacity);
        Assert.IsType<LightningOrb>(Assert.Single(player.PlayerCombatState.OrbQueue.Orbs));
        Assert.Equal(1, player.PlayerCombatState.TurnNumber);
        await room.Engine.EndPlayerTurnAsync();
        Assert.Equal(2, player.PlayerCombatState.TurnNumber);
        Assert.IsType<LightningOrb>(Assert.Single(player.PlayerCombatState.OrbQueue.Orbs));
    }
}

[Collection("ModelDb")]
public sealed class DefectPoolTests : IDisposable
{
    // Transcribed independently from v0.111.0 DefectCardPool.GenerateAllCards.
    private const string SourceCardNames = """
        AdaptiveStrike AllForOne BallLightning Barrage BeamCell BiasedCognition BoostAway BootSequence Buffer BulkUp
        Capacitor Chaos ChargeBattery Chill Claw ColdSnap Compact CompileDriver ConsumingShadow Coolant Coolheaded
        CreativeAi Darkness DefendDefect Defragment DoubleEnergy Dualcast EchoForm EnergySurge Feral FightThrough
        FlakCannon FocusedStrike Ftl Fusion GeneticAlgorithm Glacier Glasswork GoForTheEyes GunkUp Hailstorm
        HelixDrill Hibernate Hologram Hotfix Hyperbeam IceLance Ignition ImitationLearning Iteration Leap LightningRod
        Loop MachineLearning MeteorStrike Modded MomentumStrike MultiCast Null OneForAll Overclock Quadcast Rainbow
        Reboot Refract RocketPunch Scavenge Scrape ShadowShield Shatter SignalBoost Skim Smokestack Spinner Storm
        StrikeDefect Subroutine Sunder Supercritical SweepingBeam Synchronize Synthesis Tempest TeslaCoil Thunder
        TrashToTreasure Turbo Uproar Voltaic WhiteNoise Zap
        """;

    public DefectPoolTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PoolsAndSelection_MatchSource()
    {
        string[] sourceCards = SourceCardNames.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Defect defect = ModelDb.Character<Defect>();
        CardModel[] actualCards = defect.CardPool.AllCards.ToArray();
        Assert.Equal(91, sourceCards.Length);
        Assert.Equal(sourceCards, actualCards.Select(card => card.GetType().Name));
        Assert.Equal(91, actualCards.Select(card => card.Id).Distinct().Count());
        Assert.All(actualCards, card => Assert.True(card.IsUpgradable, card.GetType().Name));
        Assert.Equal(new[] { "CrackedCore", "DataDisk", "EmotionChip", "GoldPlatedCables",
                "PowerCell", "Metronome", "RunicCapacitor", "SymbioticVirus" },
            defect.RelicPool.AllRelics.Select(relic => relic.GetType().Name));
        Assert.Equal(new[] { "FocusPotion", "EssenceOfDarkness", "PotionOfCapacity" },
            defect.PotionPool.AllPotions.Select(potion => potion.GetType().Name));

        PlayerUnlockState unlocked = PlayerUnlockState.AllUnlocked();
        Assert.Equal(new[] { "Ironclad", "Silent", "Regent", "Necrobinder", "Defect" },
            ModelDb.AllCharacters.Select(character => character.GetType().Name));
        Assert.Equal(new[]
            {
                "IroncladCardPool", "SilentCardPool", "RegentCardPool", "NecrobinderCardPool", "DefectCardPool",
            },
            unlocked.CharacterCardPools.Select(pool => pool.GetType().Name));
        Assert.Equal(sourceCards.Where(name => !actualCards.Single(card => card.GetType().Name == name).IsMultiplayerOnly),
            defect.CardPool.GetUnlockedCards(unlocked, isMultiplayer: false).Select(card => card.GetType().Name));
        Assert.Equal(3, defect.PotionPool.GetUnlockedPotions(unlocked).Count());

        var run = new RunState("defect-pool-isolation", new Overgrowth());
        Player owner = Player.CreateForNewRun(defect, run);
        run.AddPlayer(owner);
        HashSet<Type> ownTypes = actualCards.Select(card => card.GetType()).ToHashSet();
        foreach (CardRarity rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
        {
            CardModel[] rewards = CardFactory.CreateForReward(owner, 3, defect.CardPool, rarity).ToArray();
            Assert.Equal(3, rewards.Length);
            Assert.All(rewards, card => { Assert.Contains(card.GetType(), ownTypes); Assert.Equal(rarity, card.Rarity); });
            CardModel source = (CardModel)actualCards.First(card => card.Rarity == rarity).MutableClone();
            source.AssignOwner(owner);
            CardModel transformed = CardFactory.CreateRandomCardForTransform(source, false, new Rng(12345UL));
            Assert.Contains(transformed.GetType(), ownTypes);
            Assert.NotEqual(source.GetType(), transformed.GetType());
        }
        CardModel[] shop = MerchantInventory.Generate(owner).Cards.Take(5).Select(entry => entry.Card).ToArray();
        Assert.Equal(5, shop.Length);
        Assert.All(shop, card => Assert.Contains(card.GetType(), ownTypes));
        foreach (CharacterModel other in new CharacterModel[]
                 { ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Regent>() })
        {
            HashSet<Type> otherTypes = other.CardPool.AllCards.Select(card => card.GetType()).ToHashSet();
            Assert.Empty(ownTypes.Intersect(otherTypes));
            var otherRun = new RunState($"old-role-{other.Id.Entry}", new Overgrowth());
            Player oldOwner = Player.CreateForNewRun(other, otherRun);
            otherRun.AddPlayer(oldOwner);
            Assert.All(CardFactory.CreateForReward(oldOwner, 3, other.CardPool, CardRarity.Common),
                card => Assert.Contains(card.GetType(), otherTypes));
            Assert.All(MerchantInventory.Generate(oldOwner).Cards.Take(5),
                entry => Assert.Contains(entry.Card.GetType(), otherTypes));
        }

        // The native Defect-only roll precedes each bundle's ordinary card draws.
        string? specialSeed = null;
        for (int i = 0; i < 10_000 && specialSeed is null; i++)
        {
            string candidateSeed = $"defect-scroll-claw-{i}";
            ulong runSeed = new RunRngSet(candidateSeed).Seed;
            if (new PlayerRngSet(runSeed).Rewards.NextInt(100) == 0) specialSeed = candidateSeed;
        }
        Assert.NotNull(specialSeed);
        var specialRun = new RunState(specialSeed, new Overgrowth());
        Player specialOwner = Player.CreateForNewRun(defect, specialRun);
        specialRun.AddPlayer(specialOwner);
        var bundles = ScrollBoxes.GenerateRandomBundles(specialOwner);
        Assert.Equal(2, bundles.Count);
        Assert.Equal(3, bundles[0].Count);
        Assert.All(bundles[0], card => Assert.IsType<Claw>(card));
        Assert.Same(bundles[0][0], bundles[0][1]); // Native bundle repeats one canonical model.
        Assert.Same(bundles[0][0], bundles[0][2]);

        var obtainRun = new RunState(specialSeed, new Overgrowth());
        obtainRun.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        Player obtainOwner = Player.CreateForNewRun(defect, obtainRun);
        obtainRun.AddPlayer(obtainOwner);
        await RelicCmd.Obtain(ModelDb.Relic<ScrollBoxes>(), obtainOwner);
        Claw[] acquired = obtainOwner.Deck.Cards.OfType<Claw>().ToArray();
        Assert.Equal(3, acquired.Length);
        Assert.Equal(3, acquired.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(acquired, card => Assert.Same(obtainOwner, card.Owner));
    }
}

[Collection("ModelDb")]
public sealed class DefectCardBehaviorTests : IDisposable
{
    public DefectCardBehaviorTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("A:Zap+")]
    [InlineData("A:Dualcast+")]
    [InlineData("A:BallLightning+")]
    [InlineData("A:Claw+")]
    [InlineData("A:Chaos+")]
    [InlineData("B:Fusion+")]
    [InlineData("B:GeneticAlgorithm+")]
    [InlineData("B:Glacier+")]
    [InlineData("B:Hyperbeam+")]
    [InlineData("C:Rainbow+")]
    [InlineData("C:RocketPunch+")]
    [InlineData("C:Turbo+")]
    [InlineData("C:Voltaic+")]
    [InlineData("C:Tempest")]
    public async Task PlayAndUpgrade_MatchesSource(string scenario)
    {
        (Player player, CombatRoom room) = await DefectTestFixture.CreateCombatAsync(scenario);
        var combat = player.PlayerCombatState!;
        var state = room.Engine.State;
        var enemy = state.HittableEnemies.Single();
        combat.OrbQueue.Clear();
        combat.OrbQueue.AddCapacity(scenario == "C:Tempest" ? 5 : 3);
        Type cardType = scenario[2..].TrimEnd('+') switch
        {
            "Zap" => typeof(Zap), "Dualcast" => typeof(Dualcast),
            "BallLightning" => typeof(BallLightning), "Claw" => typeof(Claw),
            "Chaos" => typeof(Chaos), "Fusion" => typeof(Fusion),
            "GeneticAlgorithm" => typeof(GeneticAlgorithm), "Glacier" => typeof(Glacier),
            "Hyperbeam" => typeof(Hyperbeam), "Rainbow" => typeof(Rainbow),
            "RocketPunch" => typeof(RocketPunch), "Turbo" => typeof(Turbo),
            "Voltaic" => typeof(Voltaic), "Tempest" => typeof(Tempest),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        CardModel card = DefectTestFixture.AddToPile(player, cardType, PileType.Hand,
            upgraded: scenario.EndsWith('+'));
        if (card is Tempest)
        {
            await RelicCmd.Obtain(ModelDb.Relic<ChemicalX>(), player);
            await PowerCmd.Apply<FreeSkillPower>(state, player.Creature, 1m, player.Creature, null);
            combat.Energy = 2;
            Assert.Equal(0, card.EnergyCost);
        }
        if (card is Dualcast or Voltaic) await OrbCmd.Channel<LightningOrb>(state, player);
        if (card is RocketPunch)
        {
            DefectTestFixture.AddToPile(player, typeof(StrikeDefect), PileType.Draw);
            DefectTestFixture.AddToPile(player, typeof(DefendDefect), PileType.Draw);
        }
        int hpBefore = enemy.CurrentHp;
        int energyBefore = combat.Energy;
        int orbRngBefore = player.RunState.Rng.CombatOrbGeneration.Counter;
        await room.Engine.PlayCardAsync(player, card,
            card.TargetType == TargetType.AnyEnemy ? enemy : null);
        switch (scenario)
        {
            case "A:Zap+": Assert.Equal(0, card.EnergyCost); Assert.IsType<LightningOrb>(Assert.Single(combat.OrbQueue.Orbs)); break;
            case "A:Dualcast+": Assert.Equal(0, card.EnergyCost); Assert.Empty(combat.OrbQueue.Orbs); Assert.Equal(hpBefore - 16, enemy.CurrentHp); break;
            case "A:BallLightning+": Assert.Equal(hpBefore - 10, enemy.CurrentHp); Assert.IsType<LightningOrb>(Assert.Single(combat.OrbQueue.Orbs)); break;
            case "A:Claw+": Assert.Equal(hpBefore - 4, enemy.CurrentHp); Assert.Equal(7d, ((ICardChoiceBaseValueProvider)card).CardChoiceBaseValues!.Value.Damage); break;
            case "A:Chaos+": Assert.Equal(2, combat.OrbQueue.Orbs.Count); Assert.Equal(orbRngBefore + 2, player.RunState.Rng.CombatOrbGeneration.Counter); break;
            case "B:Fusion+": Assert.False(card.HasKeyword(CardKeyword.Exhaust)); Assert.IsType<PlasmaOrb>(Assert.Single(combat.OrbQueue.Orbs)); break;
            case "B:GeneticAlgorithm+": Assert.Equal(1, player.Creature.Block); Assert.Equal(5, ((GeneticAlgorithm)card).CurrentBlock); break;
            case "B:Glacier+": Assert.Equal(9, player.Creature.Block); Assert.Equal(2, combat.OrbQueue.Orbs.Count); Assert.All(combat.OrbQueue.Orbs, orb => Assert.IsType<FrostOrb>(orb)); break;
            case "B:Hyperbeam+": Assert.Equal(hpBefore - 30, enemy.CurrentHp); Assert.Equal(-3m, player.Creature.GetPower<FocusPower>()!.Amount); break;
            case "C:Rainbow+": Assert.False(card.HasKeyword(CardKeyword.Exhaust)); Assert.Equal(new[] { typeof(LightningOrb), typeof(FrostOrb), typeof(DarkOrb) }, combat.OrbQueue.Orbs.Select(orb => orb.GetType())); break;
            case "C:RocketPunch+": Assert.Equal(hpBefore - 14, enemy.CurrentHp); Assert.Equal(2, combat.Hand.Cards.Count); break;
            case "C:Turbo+": Assert.Equal(energyBefore + 3, combat.Energy); Assert.Single(combat.DiscardPile.Cards.OfType<Sts2Sim.Core.Models.Cards.Void>()); break;
            case "C:Tempest": Assert.Equal(4, combat.OrbQueue.Orbs.Count); Assert.Equal(0, combat.Energy); break;
            case "C:Voltaic+":
                // The starting CrackedCore channel remains in native combat history after queue removal.
                Assert.Equal(3, combat.OrbQueue.Orbs.Count);
                Assert.All(combat.OrbQueue.Orbs, orb => Assert.IsType<LightningOrb>(orb));
                Assert.Equal(4, state.SemanticHistory.CountOrbsChanneledThisCombat<LightningOrb>(state, player));
                break;
        }
    }
}

[Collection("ModelDb")]
public sealed class DefectContentBehaviorTests : IDisposable
{
    public DefectContentBehaviorTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("focus-potion")]
    [InlineData("essence-of-darkness")]
    [InlineData("potion-of-capacity")]
    [InlineData("data-disk")]
    [InlineData("gold-plated-cables")]
    [InlineData("power-cell")]
    [InlineData("runic-capacitor")]
    [InlineData("symbiotic-virus")]
    [InlineData("metronome")]
    [InlineData("temporary-focus")]
    [InlineData("emotion-chip")]
    [InlineData("archaic-tooth")]
    public async Task PowersRelicsPotions_MatchSource(string behavior)
    {
        if (behavior == "archaic-tooth")
        {
            var run = new RunState("defect-archaic-tooth", new Overgrowth());
            Player owner = Player.CreateForNewRun(ModelDb.Character<Defect>(), run);
            run.AddPlayer(owner);
            Dualcast original = Assert.Single(owner.Deck.Cards.OfType<Dualcast>());
            var dustyTome = (DustyTome)ModelDb.Relic<DustyTome>().MutableClone();
            dustyTome.SetupForPlayer(owner);
            Assert.Equal(ModelDb.Card<BiasedCognition>().Id, dustyTome.AncientCard);
            await RelicCmd.Obtain(ModelDb.Relic<ArchaicTooth>(), owner);
            Assert.DoesNotContain(original, owner.Deck.Cards);
            Assert.Single(owner.Deck.Cards.OfType<Quadcast>());
            return;
        }

        if (behavior == "power-cell")
        {
            var openingRun = new RunState("defect-power-cell", new Overgrowth());
            Player openingOwner = Player.CreateForNewRun(ModelDb.Character<Defect>(), openingRun);
            openingRun.AddPlayer(openingOwner);
            foreach (Type type in new[] { typeof(Claw), typeof(Turbo) })
            {
                CardModel zeroCost = (CardModel)ModelDb.Get(type).MutableClone();
                zeroCost.AssignOwner(openingOwner);
                await CardPileCmd.AddToDeck(zeroCost);
            }
            await RelicCmd.Obtain(ModelDb.Relic<PowerCell>(), openingOwner);
            var openingRoom = new CombatRoom(() =>
                (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
            await openingRoom.Enter(openingRun);
            Assert.Contains(openingOwner.PlayerCombatState!.Hand.Cards, card => card is Claw);
            Assert.Contains(openingOwner.PlayerCombatState.Hand.Cards, card => card is Turbo);
            return;
        }

        (Player player, CombatRoom room) = await DefectTestFixture.CreateCombatAsync(behavior);
        CombatState state = room.Engine.State;
        var combat = player.PlayerCombatState!;
        var enemy = state.HittableEnemies.Single();
        combat.OrbQueue.Clear();
        combat.OrbQueue.AddCapacity(3);
        switch (behavior)
        {
            case "focus-potion":
                await DefectTestFixture.UsePotion<FocusPotion>(player);
                Assert.Equal(2m, player.Creature.GetPower<FocusPower>()!.Amount);
                break;
            case "essence-of-darkness":
                await DefectTestFixture.UsePotion<EssenceOfDarkness>(player);
                Assert.Equal(3, combat.OrbQueue.Orbs.Count);
                Assert.All(combat.OrbQueue.Orbs, orb => Assert.IsType<DarkOrb>(orb));
                break;
            case "potion-of-capacity":
                await DefectTestFixture.UsePotion<PotionOfCapacity>(player);
                Assert.Equal(5, combat.OrbQueue.Capacity);
                break;
            case "data-disk":
                await RelicCmd.Obtain(ModelDb.Relic<DataDisk>(), player);
                await player.Relics.OfType<DataDisk>().Single().AfterRoomEntered(room);
                Assert.Equal(1m, player.Creature.GetPower<FocusPower>()!.Amount);
                break;
            case "gold-plated-cables":
                await RelicCmd.Obtain(ModelDb.Relic<GoldPlatedCables>(), player);
                await OrbCmd.Channel<LightningOrb>(state, player);
                int hpBefore = enemy.CurrentHp;
                await combat.OrbQueue.Orbs[0].TriggerPassive(state, null);
                Assert.Equal(hpBefore - 6, enemy.CurrentHp);
                break;
            case "runic-capacitor":
                await RelicCmd.Obtain(ModelDb.Relic<RunicCapacitor>(), player);
                await player.Relics.OfType<RunicCapacitor>().Single().AfterSideTurnStart(
                    CombatSide.Player, [player.Creature]);
                Assert.Equal(6, combat.OrbQueue.Capacity);
                break;
            case "symbiotic-virus":
                await RelicCmd.Obtain(ModelDb.Relic<SymbioticVirus>(), player);
                await player.Relics.OfType<SymbioticVirus>().Single().AfterSideTurnStart(
                    CombatSide.Player, [player.Creature]);
                Assert.IsType<DarkOrb>(Assert.Single(combat.OrbQueue.Orbs));
                break;
            case "metronome":
                await RelicCmd.Obtain(ModelDb.Relic<Metronome>(), player);
                await OrbCmd.AddSlots(player, 7);
                int oldHp = enemy.CurrentHp;
                for (int i = 0; i < 7; i++) await OrbCmd.Channel<FrostOrb>(state, player);
                Assert.Equal(oldHp - 30, enemy.CurrentHp);
                break;
            case "temporary-focus":
                await PowerCmd.Apply<FocusedStrikePower>(state, player.Creature, 2m, player.Creature, null);
                Assert.Equal(2m, player.Creature.GetPower<FocusPower>()!.Amount);
                await player.Creature.GetPower<FocusedStrikePower>()!.AfterSideTurnEnd(
                    CombatSide.Player, [player.Creature]);
                Assert.Equal(0m, player.Creature.GetPower<FocusPower>()?.Amount ?? 0m);
                break;
            case "emotion-chip":
                await RelicCmd.Obtain(ModelDb.Relic<EmotionChip>(), player);
                await OrbCmd.Channel<FrostOrb>(state, player);
                await CreatureCmd.Damage(state, [player.Creature], 1m,
                    ValueProp.Unblockable | ValueProp.Unpowered, enemy, null, null);
                await room.Engine.EndPlayerTurnAsync();
                Assert.True(player.Creature.Block >= 2m);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(behavior));
        }
    }
}

internal static class DefectTestFixture
{
    public static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var run = new RunState($"defect-{seed}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Defect>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        foreach (var pile in player.PlayerCombatState!.AllPiles)
            foreach (CardModel card in pile.Cards.ToArray()) CardPileCmd.Remove(card);
        player.PlayerCombatState.Energy = 99;
        return (player, room);
    }

    public static CardModel AddToPile(Player player, Type type, PileType pile, bool upgraded = false)
    {
        CardModel card = (CardModel)ModelDb.Get(type).MutableClone();
        card.AssignOwner(player);
        if (upgraded) CardCmd.Upgrade(card);
        CardPileCmd.Add(card, pile);
        return card;
    }

    public static async Task UsePotion<TPotion>(Player player) where TPotion : PotionModel
    {
        TPotion potion = (TPotion)ModelDb.Potion<TPotion>().MutableClone();
        player.AddPotionInternal(potion);
        await PotionCmd.Use(potion, player, player.Creature);
        Assert.DoesNotContain(potion, player.PotionSlots);
    }
}
