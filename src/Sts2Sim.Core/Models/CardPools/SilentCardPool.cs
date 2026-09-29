using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>The v0.111.0 Silent card pool in native GenerateAllCards order.</summary>
public sealed class SilentCardPool : CardPoolModel
{
    // Order is gameplay-visible: seeded selection indexes this sequence after rarity and unlock filtering.
    private static readonly Type[] OfficialCardTypes =
    [
        typeof(Abrasive), typeof(Accelerant), typeof(Accuracy), typeof(Acrobatics),
        typeof(Adrenaline), typeof(Afterimage), typeof(Anticipate), typeof(Assassinate),
        typeof(Backflip), typeof(Backstab), typeof(BladeOfInk), typeof(BladeDance),
        typeof(BladeSymphony), typeof(Blur), typeof(BouncingFlask), typeof(BubbleBubble),
        typeof(BulletTime), typeof(Burst), typeof(CalculatedGamble), typeof(CloakAndDagger),
        typeof(CorrosiveWave), typeof(Concoct), typeof(DaggerSpray), typeof(DaggerThrow),
        typeof(Dash), typeof(DeadlyPoison), typeof(DefendSilent), typeof(Deflect),
        typeof(DodgeAndRoll), typeof(EchoingSlash), typeof(Envenom), typeof(EscapePlan),
        typeof(Expertise), typeof(Expose), typeof(Fade), typeof(FanOfKnives),
        typeof(Finisher), typeof(Flanking), typeof(Flechettes), typeof(FlickFlack),
        typeof(Sidestep), typeof(Footwork), typeof(GrandFinale), typeof(HandTrick),
        typeof(Haze), typeof(HiddenDaggers), typeof(InfiniteBlades), typeof(KnifeTrap),
        typeof(LeadingStrike), typeof(LegSweep), typeof(Malaise), typeof(MasterPlanner),
        typeof(MementoMori), typeof(Mirage), typeof(Murder), typeof(Neutralize),
        typeof(Nightmare), typeof(NoxiousFumes), typeof(Outbreak), typeof(PhantomBlades),
        typeof(PiercingWail), typeof(Pinpoint), typeof(PoisonedStab), typeof(Pounce),
        typeof(PreciseCut), typeof(Predator), typeof(Prepared), typeof(Reflex),
        typeof(Ricochet), typeof(SerpentForm), typeof(ShadowStep), typeof(Shadowmeld),
        typeof(Skewer), typeof(Slice), typeof(Snakebite), typeof(Sneaky),
        typeof(Speedster), typeof(StormOfSteel), typeof(Strangle), typeof(StrikeSilent),
        typeof(SuckerPunch), typeof(Suppress), typeof(Survivor), typeof(Tactician),
        typeof(TheHunt), typeof(ToolsOfTheTrade), typeof(Tracking), typeof(Untouchable),
        typeof(UpMySleeve), typeof(WellLaidPlans), typeof(WraithForm),
    ];

    public override IReadOnlyList<CardModel> AllCards { get; } = OfficialCardTypes
        .Select(type => (CardModel)ModelDb.Get(type)).ToList().AsReadOnly();
}
