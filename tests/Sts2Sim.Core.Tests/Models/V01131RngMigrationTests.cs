using System.Reflection;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class V01131RngMigrationTests : IDisposable
{
    public V01131RngMigrationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static IEnumerable<object[]> Cases()
    {
        string[] noncombat = ["EntropicBrew", "AlchemicalCoffer", "DelicateFrond", "PhialHolster",
            "FragrantMushroom", "Kaleidoscope", "Whetstone", "NeowsBones", "NeowsBonesCurse",
            "DistinguishedCape", "RoyalStamp", "FishingRod", "WarPaint", "SandCastle",
            "WarHammer", "BeautifulBracelet"];
        foreach (string effect in noncombat)
        {
            yield return [effect, false];
            yield return [effect, true];
        }
        foreach (string effect in new[] { "BeatDown", "Uproar", "Catastrophe", "StampedePower", "EntropyPower", "ThievingHopper" })
            yield return [effect, false];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExistingConsumers_UseNativeStream(string effect, bool keyed)
    {
        string seed = "918-" + effect;
        var run = keyed ? RunState.CreateKeyedForLabels(seed, [new Overgrowth()]) : new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var selection = new LegacySelectionDecisionSource();
        run.ConfigureCardSelectionSource(selection);
        bool combat = effect is "BeatDown" or "Uproar" or "Catastrophe" or "StampedePower" or "EntropyPower" or "ThievingHopper";
        CombatRoom? room = null;
        if (combat)
        {
            room = new CombatRoom(() => (MonsterModel)(effect == "ThievingHopper"
                ? (MonsterModel)ModelDb.Monster<ThievingHopper>() : ModelDb.Monster<WanderingGrunt>()).MutableClone());
            room.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
            await room.Enter(run);
            foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToArray())
                CardPileCmd.Remove(card);
            PileType pile = effect == "BeatDown" ? PileType.Discard
                : effect is "StampedePower" or "EntropyPower" ? PileType.Hand : PileType.Draw;
            for (int i = 0; i < 3; i++)
            {
                var card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
                card.AssignOwner(player);
                card.AssignDeckVersionInternal(player.Deck.Cards[i]);
                CardPileCmd.Add(card, pile);
            }
        }
        else if (effect is "FishingRod" or "WarHammer")
            run.PushRoom(new CombatRoom(() => (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(),
                effect == "WarHammer" ? RoomType.Elite : RoomType.Monster));
        else if (effect is "NeowsBones" or "Kaleidoscope")
        {
            var ancient = new EventRoom(() => (Neow)ModelDb.Get(typeof(Neow)).MutableClone());
            run.PushRoom(ancient);
            await ancient.Enter(run);
        }

        int draws = effect switch
        {
            "EntropicBrew" or "DelicateFrond" => 2 * player.MaxPotionCount,
            "AlchemicalCoffer" => 8,
            "PhialHolster" => 4,
            "DistinguishedCape" => 2,
            "FishingRod" or "NeowsBonesCurse" => 1,
            // v0.113.1（#946）：每次 UnstableShuffle / StableShuffle / TakeRandom 只从父流取一次
            // NextUnsignedLong 派生子 Rng，与候选数无关；下列值是各效果的洗牌调用次数。
            "Whetstone" or "WarPaint" or "RoyalStamp" or "BeautifulBracelet" or "NeowsBones" => 1,
            "Kaleidoscope" => 2,
            "BeatDown" or "Uproar" => 1,
            "Catastrophe" => 2,
            "StampedePower" or "EntropyPower" or "ThievingHopper" => 1,
            _ => 1, // FragrantMushroom / SandCastle / WarHammer：各一次 StableShuffle
        };
        int rewardsBefore = player.PlayerRng.Rewards.Counter;
        Rng rewardOracle = player.PlayerRng.Rewards.CloneExact();
        var counters = Enum.GetValues<RunRngType>().ToDictionary(type => type, type => run.Rng.GetRng(type).Counter);
        int noncombatBefore = player.PlayerRng.NonCombatGeneration.Counter;
        Rng noncombatOracle = keyed ? new Rng(0) : player.PlayerRng.NonCombatGeneration.CloneExact();
        for (int i = 0; i < draws; i++) noncombatOracle.NextInt();
        int traceBefore = player.PlayerRng.GetKeyedDraws().Count;

        if (combat)
        {
            CombatState state = room!.Engine.State;
            if (effect == "StampedePower")
            {
                var power = (StampedePower)ModelDb.Power<StampedePower>().MutableClone();
                power.ApplyInternal(player.Creature, 1);
                await power.AfterAutoPostPlayPhaseEntered(player);
            }
            else if (effect == "EntropyPower")
            {
                var power = (EntropyPower)ModelDb.Power<EntropyPower>().MutableClone();
                power.ApplyInternal(player.Creature, 1);
                await power.AfterPlayerTurnStart(player);
            }
            else if (effect == "ThievingHopper")
            {
                var monster = state.Enemies.Single().Monster!;
                await (Task)typeof(ThievingHopper).GetMethod("Theft", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(monster, [new Creature[] { player.Creature }])!;
            }
            else
            {
                var card = (CardModel)ModelDb.All<CardModel>().Single(card => card.GetType().Name == effect).MutableClone();
                card.AssignOwner(player);
                CardPileCmd.Add(card, PileType.Hand);
                await card.PlayAsync(card.TargetType == TargetType.AnyEnemy ? state.HittableEnemies.Single() : null);
            }
            RunRngType expected = effect == "EntropyPower" ? RunRngType.CombatCardGeneration : RunRngType.CombatCardSelection;
            Assert.Equal(counters[expected] + draws, run.Rng.GetRng(expected).Counter);
            Assert.Equal(counters[RunRngType.Shuffle], run.Rng.Shuffle.Counter);
            RunRngType old = effect == "ThievingHopper" ? RunRngType.CombatCardGeneration : RunRngType.CombatCardSelection;
            if (old != expected) Assert.Equal(counters[old], run.Rng.GetRng(old).Counter);
        }
        else if (effect == "EntropicBrew")
        {
            var potion = (EntropicBrew)ModelDb.Potion<EntropicBrew>().MutableClone();
            potion.AssignOwner(player);
            await potion.UseInternal(player.Creature);
        }
        else if (effect == "NeowsBonesCurse")
            await new NeowsBonesCurseReward(player).Take();
        else
        {
            RelicModel canonical = ModelDb.All<RelicModel>().Single(relic => relic.GetType().Name == effect);
            await RelicCmd.Obtain(canonical, player);
            RelicModel relic = player.Relics.Single(relic => relic.GetType() == canonical.GetType());
            if (effect == "DelicateFrond") await relic.BeforeCombatStart();
            if (effect == "WarHammer") await relic.AfterCombatVictory();
            if (effect == "FishingRod") for (int i = 0; i < 3; i++) await relic.AfterCombatEnd();
            if (effect == "RoyalStamp")
            {
                // #1044: game CardSelectCmd.FromDeckForEnchantment orders the shuffled list by deck index before showing it.
                CardSelectionRequest request = Assert.Single(selection.Requests, request => request.Source is RoyalStamp);
                CardModel[] deckOrder = player.Deck.Cards.Where(card => request.Candidates.Contains(card)).ToArray();
                Assert.True(request.Candidates.SequenceEqual(deckOrder, ReferenceEqualityComparer.Instance),
                    $"seed {seed}: RoyalStamp candidates must follow deck order (CardSelectCmd.FromDeckForEnchantment)");
            }
        }

        if (!combat)
        {
            Assert.All(counters, entry => Assert.Equal(entry.Value, run.Rng.GetRng(entry.Key).Counter));
            if (keyed)
            {
                var newDraws = player.PlayerRng.GetKeyedDraws().Skip(traceBefore).Where(draw => draw.StreamName == "non_combat_generation").ToArray();
                Assert.Equal(draws, newDraws.Length);
                Assert.Equal(noncombatBefore, player.PlayerRng.NonCombatGeneration.Counter);
            }
            else
            {
                Assert.Equal(noncombatBefore + draws, player.PlayerRng.NonCombatGeneration.Counter);
                Assert.Equal(noncombatOracle.NextInt(), player.PlayerRng.NonCombatGeneration.NextInt());
            }
        }
        // Kaleidoscope intentionally keeps card rarity/selection/upgrade on Rewards;
        // only its pool permutation moved to NonCombatGeneration.
        if (!keyed && effect != "Kaleidoscope")
        {
            Assert.Equal(rewardsBefore, player.PlayerRng.Rewards.Counter);
            Assert.Equal(rewardOracle.NextInt(), player.PlayerRng.Rewards.NextInt());
        }
    }
}
