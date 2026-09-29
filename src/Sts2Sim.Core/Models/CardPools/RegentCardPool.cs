using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>The official Regent pool in decompiled source order.</summary>
public sealed class RegentCardPool : CardPoolModel
{
    public const string Regent2EpochId = "REGENT2_EPOCH";
    public const string Regent5EpochId = "REGENT5_EPOCH";
    public const string Regent7EpochId = "REGENT7_EPOCH";

    private static readonly Type[] Regent2Cards =
    {
        typeof(SpoilsOfBattle), typeof(SwordSage), typeof(Furnace),
    };

    private static readonly Type[] Regent5Cards =
    {
        typeof(Begone), typeof(Arsenal), typeof(Supermassive),
    };

    private static readonly Type[] Regent7Cards =
    {
        typeof(Patter), typeof(HeavenlyDrill), typeof(LunarBlast),
    };

    private static readonly Type[] OfficialCardTypes =
    {
        typeof(Alignment), typeof(Arsenal), typeof(AstralPulse), typeof(BeatIntoShape),
        typeof(Begone), typeof(BigBang), typeof(BlackHole), typeof(Bombardment), typeof(Bulwark),
        typeof(BundleOfJoy), typeof(CelestialMight), typeof(Charge), typeof(ChildOfTheStars),
        typeof(CloakOfStars), typeof(CollisionCourse), typeof(Comet), typeof(Conqueror),
        typeof(Constellation), typeof(Convergence), typeof(CosmicIndifference), typeof(CrashLanding),
        typeof(CrescentSpear), typeof(CrushUnder), typeof(DecisionsDecisions), typeof(DefendRegent),
        typeof(Devastate), typeof(DyingStar), typeof(FallingStar), typeof(ForegoneConclusion),
        typeof(Furnace), typeof(GammaBlast), typeof(GatherLight), typeof(Genesis), typeof(Glimmer),
        typeof(Glitterstream), typeof(Glow), typeof(Guards), typeof(GuidingStar), typeof(HammerTime),
        typeof(HeavenlyDrill), typeof(Hegemony), typeof(HeirloomHammer), typeof(HiddenCache),
        typeof(IAmInvincible), typeof(KinglyKick), typeof(KinglyPunch), typeof(KnockoutBlow),
        typeof(KnowThyPlace), typeof(Largesse), typeof(LunarBlast), typeof(MakeItSo),
        typeof(ManifestAuthority), typeof(MeteorShower), typeof(MonarchsGaze), typeof(Monologue),
        typeof(NeutronAegis), typeof(Orbit), typeof(PaleBlueDot), typeof(Parry), typeof(ParticleWall),
        typeof(Patter), typeof(PhotonCut), typeof(Plot), typeof(PillarOfCreation), typeof(Prophesize),
        typeof(Quasar), typeof(Radiate), typeof(RefineBlade), typeof(Reflect), typeof(Resonance),
        typeof(RoyalGamble), typeof(Royalties), typeof(SeekingEdge), typeof(SevenStars),
        typeof(ShiningStrike), typeof(SolarStrike), typeof(SpectrumShift), typeof(SpoilsOfBattle),
        typeof(Stardust), typeof(StrikeRegent), typeof(SummonForth), typeof(Supermassive),
        typeof(SwordSage), typeof(Terraforming), typeof(TheSealedThrone), typeof(TheSmith),
        typeof(Tutor), typeof(Tyranny), typeof(Venerate), typeof(VoidForm), typeof(WroughtInWar),
    };

    public override IReadOnlyList<CardModel> AllCards => OfficialCardTypes
        .Where(ModelDb.Contains)
        .Select(type => (CardModel)ModelDb.Get(type))
        .ToArray();

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        PlayerUnlockState unlockState,
        IEnumerable<CardModel> cards)
    {
        IEnumerable<CardModel> result = cards;
        if (!unlockState.IsEpochRevealed(Regent2EpochId))
        {
            result = Exclude(result, Regent2Cards);
        }
        if (!unlockState.IsEpochRevealed(Regent5EpochId))
        {
            result = Exclude(result, Regent5Cards);
        }
        if (!unlockState.IsEpochRevealed(Regent7EpochId))
        {
            result = Exclude(result, Regent7Cards);
        }
        return result;
    }

    private static IEnumerable<CardModel> Exclude(
        IEnumerable<CardModel> cards,
        IReadOnlyCollection<Type> excludedTypes) =>
        cards.Where(card => !excludedTypes.Contains(card.GetType()));
}
