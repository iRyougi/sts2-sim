using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using PaelsLegion = Sts2Sim.Core.Models.Relics.PaelsLegion;

namespace Sts2Sim.Core.Tests.Content;

[Collection("ModelDb")]
public sealed class ContentRegistryTests : IDisposable
{
    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void AllTypes_RegistersCompleteCardPoolsAndForgeToken()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);

        CardModel[] cards = ModelDb.All<CardModel>().ToArray();
        // Defect adds 91 pool cards and Fuel, a generated token; Necrobinder adds 91 pool cards plus the
        // Soul and SweepingGaze tokens.
        Assert.Equal(595, cards.Length);
        Assert.Equal(569, cards.Count(card => card.Rarity is not CardRarity.Token and not CardRarity.Status));
        Assert.Equal(504, cards.Count(card => !card.IsColorless && card.Rarity is not CardRarity.Token and not CardRarity.Status));
        Assert.Equal(65, cards.Count(card => card.IsColorless));
        Assert.Equal(10, cards.Count(card => card.Rarity == CardRarity.Token));
        Assert.Equal(16, cards.Count(card => card.Rarity == CardRarity.Status));
        Assert.IsType<Infection>(ModelDb.Card<Infection>());
        Assert.IsType<AscendersBane>(ModelDb.Card<AscendersBane>());
        Assert.IsType<Doubt>(ModelDb.Card<Doubt>());
        Assert.IsType<Shame>(ModelDb.Card<Shame>());
        Assert.IsType<Exterminate>(ModelDb.Card<Exterminate>());
        Assert.IsType<Squash>(ModelDb.Card<Squash>());
        Assert.IsType<Metamorphosis>(ModelDb.Card<Metamorphosis>());
        Assert.IsType<LanternKey>(ModelDb.Card<LanternKey>());
        Assert.IsType<SpoilsMap>(ModelDb.Card<SpoilsMap>());
        Assert.IsType<Enlightenment>(ModelDb.Card<Enlightenment>());
        Assert.IsType<Luminesce>(ModelDb.Card<Luminesce>());
        Assert.IsType<Relax>(ModelDb.Card<Relax>());
        Assert.IsType<BrightestFlame>(ModelDb.Card<BrightestFlame>());
        Assert.IsType<MadScience>(ModelDb.Card<MadScience>());
        Assert.IsType<Apotheosis>(ModelDb.Card<Apotheosis>());
        Assert.IsType<Whistle>(ModelDb.Card<Whistle>());
        Assert.IsType<Wish>(ModelDb.Card<Wish>());
        Assert.IsType<Maul>(ModelDb.Card<Maul>());
        Assert.IsType<Apparition>(ModelDb.Card<Apparition>());
        Type[] underdocksCardTypes =
        [
            typeof(FeedingFrenzy), typeof(Caltrops), typeof(Clash), typeof(Distraction),
            typeof(DualWield), typeof(Entrench), typeof(HelloWorld), typeof(Outmaneuver),
            typeof(Rebound), typeof(RipAndTear), typeof(Stack),
        ];
        Assert.All(underdocksCardTypes, type => Assert.IsAssignableFrom<CardModel>(ModelDb.Get(type)));
    }

    [Fact]
    public void AllTypes_RegistersCompleteRelicPool()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);

        RelicModel[] relics = ModelDb.All<RelicModel>().ToArray();
        // Exact reflection-registration baseline includes Ring of the Snake, Hive, Glory, Shovel, Girya, Task 3a
        // relics, the eight Necrobinder relics, and five Orobas upgraded starters.
        Assert.Equal(298, relics.Length);
        Assert.Equal(
            137,
            relics.Count(relic =>
                relic.Rarity is RelicRarity.Ancient or RelicRarity.Event));

        Type[] hiveRelicTypes =
        {
            typeof(PollinousCore), typeof(Sts2Sim.Core.Models.Relics.LostWisp),
            typeof(ElectricShrymp), typeof(GlassEye), typeof(AlchemicalCoffer), typeof(Driftwood),
            typeof(RadiantPearl), typeof(SandCastle), typeof(SeaGlass), typeof(PrismaticGem),
            typeof(TouchOfOrobas), typeof(ArchaicTooth),
            typeof(PaelsClaw), typeof(PaelsTooth), typeof(PaelsLegion), typeof(PaelsGrowth),
            typeof(PaelsFlesh), typeof(PaelsHorn), typeof(PaelsTears), typeof(PaelsWing),
            typeof(PaelsEye), typeof(PaelsBlood),
            typeof(NutritiousSoup), typeof(VeryHotCocoa), typeof(YummyCookie), typeof(BiiigHug),
            typeof(Storybook), typeof(ToastyMittens), typeof(GoldenCompass), typeof(PumpkinCandle),
            typeof(ToyBox), typeof(SealOfGold),
        };
        Assert.Equal(32, hiveRelicTypes.Distinct().Count());
        Assert.All(hiveRelicTypes, type => Assert.IsAssignableFrom<RelicModel>(ModelDb.Get(type)));
        Type[] gloryEventRelics =
        {
            typeof(BigMushroom), typeof(ForgottenSoul), typeof(FragrantMushroom), typeof(RoyalPoison),
        };
        Assert.All(gloryEventRelics, type => Assert.IsAssignableFrom<RelicModel>(ModelDb.Get(type)));
        Type[] underdocksRelicTypes =
        [
            typeof(DarkstonePeriapt), typeof(DreamCatcher), typeof(FresnelLens),
            typeof(HandDrill), typeof(MawBank), typeof(TheBoot), typeof(TinyMailbox),
        ];
        Assert.All(underdocksRelicTypes, type => Assert.IsAssignableFrom<RelicModel>(ModelDb.Get(type)));
    }

    [Fact]
    public void AllTypes_RegistersExactImplementedEventSet()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);

        EventModel[] events = ModelDb.All<EventModel>().ToArray();
        Assert.Equal(
            new[]
            {
                typeof(AbyssalBaths),
                typeof(Amalgamator),
                typeof(AromaOfChaos),
                typeof(BattlewornDummy),
                typeof(BrainLeech),
                typeof(Bugslayer),
                typeof(ByrdonisNest),
                typeof(ColorfulPhilosophers),
                typeof(ColossalFlower),
                typeof(CrystalSphere),
                typeof(Darv),
                typeof(DenseVegetation),
                typeof(DollRoom),
                typeof(DoorsOfLightAndDark),
                typeof(DrowningBeacon),
                typeof(EndlessConveyor),
                typeof(FakeMerchant),
                typeof(FieldOfManSizedHoles),
                typeof(GraveOfTheForgotten),
                typeof(HungryForMushrooms),
                typeof(InfestedAutomaton),
                typeof(JungleMazeAdventure),
                typeof(Sts2Sim.Core.Models.Events.LostWisp),
                typeof(LuminousChoir),
                typeof(MorphicGrove),
                typeof(Neow),
                typeof(Nonupeipe),
                typeof(Orobas),
                typeof(Pael),
                typeof(PotionCourier),
                typeof(PunchOff),
                typeof(RanwidTheElder),
                typeof(Reflections),
                typeof(RelicTrader),
                typeof(RoomFullOfCheese),
                typeof(RoundTeaParty),
                typeof(SapphireSeed),
                typeof(SelfHelpBook),
                typeof(SlipperyBridge),
                typeof(SpiralingWhirlpool),
                typeof(SpiritGrafter),
                typeof(StoneOfAllTime),
                typeof(SunkenStatue),
                typeof(SunkenTreasury),
                typeof(Symbiote),
                typeof(TabletOfTruth),
                typeof(Tanx),
                typeof(TeaMaster),
                typeof(Tezcatara),
                typeof(TheFutureOfPotions),
                typeof(TheLanternKey),
                typeof(TheLegendsWereTrue),
                typeof(ThisOrThat),
                typeof(TinkerTime),
                typeof(TrashHeap),
                typeof(Trial),
                typeof(UnrestSite),
                typeof(Vakuu),
                typeof(WarHistorianRepy),
                typeof(WaterloggedScriptorium),
                typeof(WelcomeToWongos),
                typeof(Wellspring),
                typeof(WhisperingHollow),
                typeof(WoodCarvings),
                typeof(ZenWeaver),
            },
            events.Select(model => model.GetType()));
    }

    [Fact]
    public void AllTypes_RegistersDenseVegetationMonster()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);

        Assert.IsType<Wriggler>(ModelDb.Monster<Wriggler>());
    }

    [Fact]
    public void AllTypes_RegistersCompletePotionPool()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);

        PotionModel[] potions = ModelDb.All<PotionModel>().ToArray();
        // 基线 50，两侧各自新增后合并：
        // +3 偏离 #299 的猎手专属药水 PoisonPotion(C) / CunningPotion(U) / GhostInAJar(R)；
        // +1 暗港的 GlowwaterPotion(Event)。
        // +3 亡灵契约师专属药水 PotionOfDoom(C) / BoneBrew(U) / PotOfGhouls(R)。
        Assert.Equal(64, potions.Length);
        Assert.Equal(20, potions.Count(potion => potion.Rarity == PotionRarity.Common));
        Assert.Equal(20, potions.Count(potion => potion.Rarity == PotionRarity.Uncommon));
        Assert.Equal(20, potions.Count(potion => potion.Rarity == PotionRarity.Rare));
        Assert.Equal(3, potions.Count(potion => potion.Rarity == PotionRarity.Event));
        Assert.IsType<GlowwaterPotion>(ModelDb.Potion<GlowwaterPotion>());
    }

    [Fact]
    public void AllTypes_AreConcreteAndUnique()
    {
        Assert.Equal(ContentRegistry.AllTypes.Count, ContentRegistry.AllTypes.Distinct().Count());
        Assert.All(ContentRegistry.AllTypes, type =>
        {
            Assert.False(type.IsAbstract);
            Assert.True(typeof(AbstractModel).IsAssignableFrom(type));
        });
    }
}
