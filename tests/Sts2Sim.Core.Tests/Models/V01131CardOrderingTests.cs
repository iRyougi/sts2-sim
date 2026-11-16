using System.Reflection;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

/// <summary>#918：v0.113.1 原版 <c>CardModel.CompareTo</c> 的实例状态次序（源码派生，非实现生成的金值）。</summary>
[Collection("ModelDb")]
public sealed class V01131CardOrderingTests : IDisposable
{
    public V01131CardOrderingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("upgrade", -1)]
    [InlineData("enchantmentNullFirst", -1)]
    [InlineData("afflictionNullFirst", -1)]
    [InlineData("enchantmentBeforeAffliction", 1)]
    [InlineData("resolvedCost", -1)]
    [InlineData("unplayableCostResolvesToZero", 0)]
    [InlineData("xCaptureFromPlay", -1)]
    [InlineData("replayCount", 1)]
    [InlineData("mutableKey", -1)]
    [InlineData("baseValueClamp", 0)]
    [InlineData("exactClone", 0)]
    [InlineData("stableShuffleInputOrder", 0)]
    [InlineData("failClosedRegistry", 0)]
    public async Task CompareTo_UsesNativeStatePrecedence(string scenario, int expectedSign)
    {
        Player player = await EnterCombatAsync("918-order-" + scenario);
        (CardModel left, CardModel right) = await BuildAsync(scenario, player);
        Assert.True(
            Math.Sign(left.CompareTo(right)) == expectedSign && Math.Sign(right.CompareTo(left)) == -expectedSign,
            $"{scenario}: left.CompareTo(right)={left.CompareTo(right)}, reverse={right.CompareTo(left)}, expected sign {expectedSign}");
    }

    private static async Task<(CardModel Left, CardModel Right)> BuildAsync(string scenario, Player player)
    {
        switch (scenario)
        {
            case "upgrade":
            {
                StrikeSilent upgraded = Owned<StrikeSilent>(player);
                upgraded.Upgrade();
                return (Owned<StrikeSilent>(player), upgraded);
            }
            case "enchantmentNullFirst":
            {
                // Same Id/upgrade; native ModelId.Compare(null, Glam) < 0.
                StrikeSilent enchanted = InHand<StrikeSilent>(player);
                await CardCmd.Enchant<Glam>(enchanted, 1m);
                return (InHand<StrikeSilent>(player), enchanted);
            }
            case "afflictionNullFirst":
            {
                StrikeSilent afflicted = InHand<StrikeSilent>(player);
                Assert.NotNull(await CardCmd.Afflict<Hexed>(afflicted, 1m));
                return (InHand<StrikeSilent>(player), afflicted);
            }
            case "enchantmentBeforeAffliction":
            {
                // Enchantment precedes affliction: an enchanted card sorts after an afflicted unenchanted one.
                StrikeSilent enchanted = InHand<StrikeSilent>(player);
                await CardCmd.Enchant<Glam>(enchanted, 1m);
                StrikeSilent afflicted = InHand<StrikeSilent>(player);
                Assert.NotNull(await CardCmd.Afflict<Hexed>(afflicted, 1m));
                return (enchanted, afflicted);
            }
            case "resolvedCost":
            {
                StrikeSilent costly = InHand<StrikeSilent>(player);
                costly.AddEnergyCostThisCombat(1);
                Assert.Equal(2, costly.ResolvedEnergyCostForComparison);
                return (InHand<StrikeSilent>(player), costly);
            }
            case "unplayableCostResolvesToZero":
            {
                // Native GetWithModifiers returns base -1 before modifiers; GetResolved clamps to 0.
                Wither left = InHand<Wither>(player);
                Wither right = InHand<Wither>(player);
                Assert.Equal(0, left.ResolvedEnergyCostForComparison);
                return (left, right);
            }
            case "xCaptureFromPlay":
            {
                // Native SpendEnergy stores CapturedXValue on the card; it survives the play.
                Malaise played = InHand<Malaise>(player);
                player.PlayerCombatState!.Energy = 3;
                await played.PlayAsync(player.Creature.CombatState!.Enemies.First(enemy => enemy.IsAlive));
                Assert.Equal(3, played.CapturedEnergyXValue);
                Assert.Equal(3, played.ResolvedEnergyCostForComparison);
                // Native CardCmd.AutoPlay(skipXCapture: false) captures current energy without spending it.
                Malaise autoplayed = InHand<Malaise>(player);
                player.PlayerCombatState!.Energy = 2;
                await AutoPlayCmd.FromCards(player.Creature.CombatState!, player, [autoplayed]);
                Assert.Equal(2, autoplayed.CapturedEnergyXValue);
                Assert.Equal(2, player.PlayerCombatState!.Energy);
                return (InHand<Malaise>(player), played);
            }
            case "replayCount":
            {
                StrikeSilent replayed = Owned<StrikeSilent>(player);
                replayed.BaseReplayCount = 1;
                return (replayed, Owned<StrikeSilent>(player));
            }
            case "mutableKey":
            {
                SovereignBlade forged = Owned<SovereignBlade>(player);
                forged.AddDamage(5m);
                return (Owned<SovereignBlade>(player), forged);
            }
            case "baseValueClamp":
            {
                // Native BaseValue setter clamps to 999,999,999, so both compare equal.
                SovereignBlade left = Owned<SovereignBlade>(player);
                left.AddDamage(2_000_000_000m);
                SovereignBlade right = Owned<SovereignBlade>(player);
                right.AddDamage(3_000_000_000m);
                return (left, right);
            }
            case "exactClone":
            {
                Wither original = InHand<Wither>(player);
                original.FakeUpgrade();
                original.BaseReplayCount = 2;
                return (original, (CardModel)original.MutableClone());
            }
            case "stableShuffleInputOrder":
            {
                CardModel[] cards = DistinctStateStrikes(player);
                List<CardModel> forward = cards.ToList().StableShuffle(new Rng(918));
                List<CardModel> reversed = cards.Reverse().ToList().StableShuffle(new Rng(918));
                Assert.True(forward.SequenceEqual(reversed, ReferenceEqualityComparer.Instance),
                    "StableShuffle must not depend on input order when instance states differ.");
                return (forward[0], forward[0]);
            }
            case "failClosedRegistry":
            {
                AssertRegistryCoversPortedCards(player);
                return (Owned<StrikeSilent>(player), Owned<StrikeSilent>(player));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private static CardModel[] DistinctStateStrikes(Player player)
    {
        var cards = new List<CardModel>();
        for (int replay = 0; replay < 4; replay++)
        {
            StrikeSilent card = Owned<StrikeSilent>(player);
            card.BaseReplayCount = replay;
            cards.Add(card);
            StrikeSilent costly = Owned<StrikeSilent>(player);
            costly.BaseReplayCount = replay;
            costly.AddEnergyCostThisCombat(1);
            cards.Add(costly);
        }
        return cards.ToArray();
    }

    private static void AssertRegistryCoversPortedCards(Player player)
    {
        Type[] cardTypes = typeof(CardModel).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(CardModel).IsAssignableFrom(type))
            .ToArray();
        string[] unregisteredSources = cardTypes
            .Where(type => typeof(ICardComparisonBaseValueSource).IsAssignableFrom(type) &&
                           !CardComparisonBaseValues.MutableKeysByCardClass.ContainsKey(type.Name))
            .Select(type => type.Name).ToArray();
        Assert.True(unregisteredSources.Length == 0, "Unregistered sources: " + string.Join(", ", unregisteredSources));

        foreach ((string className, IReadOnlyList<string> keys) in CardComparisonBaseValues.MutableKeysByCardClass)
        {
            Type? type = cardTypes.SingleOrDefault(candidate => candidate.Name == className);
            if (type is null)
            {
                Assert.Equal("ShowOff", className); // unported: must implement the source when ported
                continue;
            }

            Assert.True(typeof(ICardComparisonBaseValueSource).IsAssignableFrom(type), className);
            var card = (CardModel)ModelDb.Get(type).MutableClone();
            card.AssignOwner(player);
            foreach (string key in keys)
            {
                _ = ((ICardComparisonBaseValueSource)card).GetComparisonBaseValue(key);
            }
            Assert.Equal(0, card.CompareTo((CardModel)card.MutableClone()));
        }

        // Unset instances report the native canonical BaseValue, not a zero placeholder.
        Assert.Equal(6m, Owned<Disintegration>(player).GetComparisonBaseValue("DisintegrationPower"));
    }

    private static TCard Owned<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static TCard InHand<TCard>(Player player) where TCard : CardModel
    {
        TCard card = Owned<TCard>(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<Player> EnterCombatAsync(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        room.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        await room.Enter(run);
        foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToArray())
        {
            CardPileCmd.Remove(card);
        }
        return player;
    }
}
