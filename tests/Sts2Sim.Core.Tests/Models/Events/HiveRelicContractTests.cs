using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using PaelsLegion = Sts2Sim.Core.Models.Relics.PaelsLegion;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class HiveRelicContractTests : IDisposable
{
    private static readonly Type[] Relics =
    [
        typeof(PollinousCore), typeof(Sts2Sim.Core.Models.Relics.LostWisp),
        typeof(ElectricShrymp), typeof(GlassEye), typeof(AlchemicalCoffer), typeof(Driftwood),
        typeof(RadiantPearl), typeof(SandCastle), typeof(SeaGlass), typeof(PrismaticGem),
        typeof(TouchOfOrobas), typeof(ArchaicTooth), typeof(PaelsClaw), typeof(PaelsTooth),
        typeof(PaelsLegion), typeof(PaelsGrowth), typeof(PaelsFlesh), typeof(PaelsHorn),
        typeof(PaelsTears), typeof(PaelsWing), typeof(PaelsEye), typeof(PaelsBlood),
        typeof(NutritiousSoup), typeof(VeryHotCocoa), typeof(YummyCookie), typeof(BiiigHug),
        typeof(Storybook), typeof(ToastyMittens), typeof(GoldenCompass), typeof(PumpkinCandle),
        typeof(ToyBox), typeof(SealOfGold),
    ];

    public HiveRelicContractTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void AllThirtyTwoRelics_AreIndividualModelsWithExactRarity()
    {
        Assert.Equal(32, Relics.Length);
        Assert.Equal(32, Relics.Distinct().Count());
        foreach (Type type in Relics)
        {
            RelicModel relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
            Assert.Equal(typeof(RelicModel), type.BaseType);
            Assert.Equal(type == typeof(PollinousCore) || type == typeof(Sts2Sim.Core.Models.Relics.LostWisp)
                ? RelicRarity.Event : RelicRarity.Ancient, relic.Rarity);
        }
    }

    [Fact]
    public async Task GlassEye_OffersFiveCardChoicesTogetherInNativeRarityOrder()
    {
        (RunState run, Player player) = CreateRun("glass-eye-offer");
        var room = new EventRoom(() => (Neow)ModelDb.Event<Neow>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        int rewardsBefore = player.PlayerRng.Rewards.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<GlassEye>(), player);

        Assert.True(room.Event.TryDequeuePendingRewardOffer(out RewardsSet? offer));
        CardReward[] choices = offer.ExtraRewards.Cast<CardReward>().ToArray();
        Assert.Equal(5, choices.Length);
        Assert.Equal(new[]
        {
            CardRarity.Common, CardRarity.Common,
            CardRarity.Uncommon, CardRarity.Uncommon, CardRarity.Rare,
        }, choices.Select(choice => Assert.Single(choice.Options.Select(card => card.Rarity).Distinct())));
        Assert.All(choices, choice => Assert.Equal(3, choice.Options.Count));
        Assert.False(room.Event.TryDequeuePendingRewardOffer(out _));
        Assert.Equal(rewardsBefore + 15, player.PlayerRng.Rewards.Counter);
    }

    [Fact]
    public async Task Goopy_EnchantsOnlyDefendAndAddsExhaust()
    {
        (RunState run, Player player) = CreateRun("goopy-core");
        DefendRegent defend = player.Deck.Cards.OfType<DefendRegent>().First();
        await CardCmd.Enchant<Goopy>(defend, 1m);
        Goopy goopy = Assert.Single(defend.Enchantments.OfType<Goopy>());
        Assert.True(defend.HasKeyword(CardKeyword.Exhaust));
        Assert.Equal(0m, goopy.EnchantBlockAdditive(8m));
    }

    [Fact]
    public async Task PickupRelics_ApplyPotionSlotsGoopyUpgradesAndRemovals()
    {
        (RunState run, Player player) = CreateRun("hive-pickups-role-10");
        int potionRng = run.Rng.CombatPotionGeneration.Counter;
        await RelicCmd.Obtain(ModelDb.Relic<AlchemicalCoffer>(), player);
        Assert.Equal(Player.InitialMaxPotionSlotCount + 4, player.MaxPotionCount);
        Assert.Equal(4, player.PotionSlots.Count(potion => potion is not null));
        Assert.Equal(8, run.Rng.CombatPotionGeneration.Counter - potionRng);
        Assert.All(player.PotionSlots.OfType<PotionModel>(),
            potion => Assert.Contains(PotionFactory.GetOutOfCombatPool(player),
                candidate => candidate.Id == potion.Id));
        ModelId[] potionIds = player.PotionSlots.OfType<PotionModel>().Select(potion => potion.Id).ToArray();
        Assert.True(potionIds.Distinct().Count() == 4,
            $"seed=hive-pickups-role-10: repeated potion in {string.Join(", ", potionIds.Select(id => id.ToString()))}");

        await RelicCmd.Obtain(ModelDb.Relic<PaelsClaw>(), player);
        Assert.All(player.Deck.Cards.Where(card => card.Tags.Contains(CardTag.Defend)),
            card => Assert.Single(card.Enchantments.OfType<Goopy>()));
        Assert.All(player.Deck.Cards.Where(card => card.Tags.Contains(CardTag.Strike)),
            card => Assert.Empty(card.Enchantments));

        int upgraded = player.Deck.Cards.Count(card => card.IsUpgraded);
        await RelicCmd.Obtain(ModelDb.Relic<YummyCookie>(), player);
        Assert.Equal(upgraded + 4, player.Deck.Cards.Count(card => card.IsUpgraded));
        int deck = player.Deck.Cards.Count;
        await RelicCmd.Obtain(ModelDb.Relic<BiiigHug>(), player);
        Assert.Equal(deck - 4, player.Deck.Cards.Count);
    }

    [Fact]
    public async Task PumpkinSealFleshAndTears_UseExactEnergyGoldAndCounterRules()
    {
        (RunState run, Player player) = CreateRun("hive-energy-state");
        await RelicCmd.Obtain(ModelDb.Relic<PumpkinCandle>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<SealOfGold>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<PaelsFlesh>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<PaelsTears>(), player);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        PumpkinCandle candle = player.Relics.OfType<PumpkinCandle>().Single();
        Assert.Equal(5, candle.KindleCount);
        Assert.Equal(player.MaxEnergy + 1, player.PlayerCombatState!.MaxEnergy);
        int gold = player.Gold;
        int energy = player.PlayerCombatState.Energy;
        await Hook.AfterEnergyReset(room.Engine.State, player);
        Assert.Equal(gold - 3, player.Gold);
        Assert.Equal(energy + 1, player.PlayerCombatState.Energy);
        player.PlayerCombatState.TurnNumber = 3;
        Assert.Equal(player.MaxEnergy + 2, player.PlayerCombatState.MaxEnergy);
        player.PlayerCombatState.Energy = 1;
        await Hook.BeforeSideTurnEnd(room.Engine.State, Sts2Sim.Core.Combat.CombatSide.Player, [player.Creature]);
        player.PlayerCombatState.Energy = 0;
        await Hook.AfterEnergyReset(room.Engine.State, player);
        Assert.Equal(3, player.PlayerCombatState.Energy);
        await candle.AfterCombatEnd();
        Assert.Equal(4, candle.KindleCount);
    }

    [Fact]
    public async Task PaelsLegion_DoublesOneCardBlockThenEntersTwoTurnCooldown()
    {
        (RunState run, Player player) = CreateRun("paels-legion");
        await RelicCmd.Obtain(ModelDb.Relic<PaelsLegion>(), player);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        player.PlayerCombatState!.Energy = 99;
        foreach (CardModel card in player.PlayerCombatState.AllPiles.SelectMany(pile => pile.Cards).ToList())
            CardPileCmd.Add(card, PileType.Discard);

        var first = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        first.AssignOwner(player);
        CardPileCmd.Add(first, PileType.Hand);
        await room.Engine.PlayCardAsync(player, first, null);
        Assert.Equal(10, player.Creature.Block);

        var second = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        second.AssignOwner(player);
        CardPileCmd.Add(second, PileType.Hand);
        await room.Engine.PlayCardAsync(player, second, null);
        Assert.Equal(15, player.Creature.Block);
    }
    private static (RunState Run, Player Player) CreateRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }
}
