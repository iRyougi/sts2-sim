using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.RelicPools;

/// <summary>The locally implemented members of the official shared relic pool.</summary>
public sealed class SharedRelicPool : RelicPoolModel
{
    public static SharedRelicPool Instance { get; } = new();

    private static readonly Type[] OfficialRelicTypes =
    {
        typeof(Akabeko), typeof(AmethystAubergine), typeof(Anchor), typeof(ArtOfWar),
        typeof(BagOfMarbles), typeof(BagOfPreparation), typeof(BeatingRemnant), typeof(Bellows),
        typeof(BeltBuckle), typeof(BloodVial), typeof(BookOfFiveRings), typeof(BowlerHat),
        typeof(Bread), typeof(BronzeScales), typeof(BurningSticks), typeof(Candelabra),
        typeof(CaptainsWheel), typeof(Cauldron), typeof(CentennialPuzzle), typeof(Chandelier), typeof(ChemicalX),
        typeof(CloakClasp), typeof(DingyRug), typeof(DollysMirror), typeof(DragonFruit), typeof(EternalFeather),
        typeof(FestivePopper), typeof(FresnelLens), typeof(FrozenEgg), typeof(GamblingChip), typeof(GamePiece), typeof(GhostSeed),
        typeof(Girya), typeof(GnarledHammer), typeof(Gorget), typeof(GremlinHorn), typeof(HappyFlower), typeof(HornCleat), typeof(IceCream),
        typeof(IntimidatingHelmet), typeof(JossPaper), typeof(JuzuBracelet), typeof(Kifuda), typeof(Kunai),
        typeof(Kusarigama), typeof(Lantern), typeof(LastingCandy), typeof(LavaLamp), typeof(LeesWaffle), typeof(LetterOpener),
        typeof(LizardTail), typeof(LoomingFruit), typeof(LuckyFysh), typeof(Mango), typeof(MealTicket), typeof(MeatOnTheBone),
        typeof(MembershipCard), typeof(MercuryHourglass), typeof(MiniatureCannon), typeof(MiniatureTent), typeof(MoltenEgg), typeof(MummifiedHand),
        typeof(MysticLighter), typeof(Nunchaku), typeof(OddlySmoothStone), typeof(OldCoin),
        typeof(Orichalcum), typeof(OrnamentalFan), typeof(Orrery), typeof(Pantograph), typeof(ParryingShield),
        typeof(Pear), typeof(PenNib), typeof(Pendulum), typeof(Permafrost), typeof(PetrifiedToad), typeof(Planisphere),
        typeof(Pocketwatch), typeof(PotionBelt), typeof(PrayerWheel), typeof(PunchDagger), typeof(RainbowRing),
        typeof(RazorTooth), typeof(RedMask), typeof(RegalPillow), typeof(ReptileTrinket),
        typeof(RingingTriangle), typeof(RippleBasin), typeof(RoyalStamp), typeof(ScreamingFlagon), typeof(Shovel), typeof(Shuriken),
        typeof(SlingOfCourage), typeof(SparklingRouge), typeof(StoneCalendar), typeof(StoneCracker),
        typeof(Strawberry), typeof(StrikeDummy), typeof(SturdyClamp), typeof(TheAbacus), typeof(TheCourier),
        typeof(TinyMailbox), typeof(Toolbox), typeof(ToxicEgg), typeof(TungstenRod), typeof(TuningFork), typeof(UnceasingTop),
        typeof(UnsettlingLamp), typeof(Vajra), typeof(Vambrace), typeof(VenerableTeaSet), typeof(VeryHotCocoa),
        typeof(VexingPuzzlebox), typeof(WarPaint), typeof(Whetstone), typeof(WhiteBeastStatue),
        typeof(WhiteStar), typeof(WingCharm),
    };

    public override IReadOnlyList<RelicModel> AllRelics => OfficialRelicTypes
        .Where(ModelDb.Contains)
        .Select(type => (RelicModel)ModelDb.Get(type))
        .ToArray();
}
