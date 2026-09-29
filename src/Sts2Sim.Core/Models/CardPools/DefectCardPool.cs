using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>Defect cards in v0.111.0 GenerateAllCards order.</summary>
public sealed class DefectCardPool : CardPoolModel
{
    private static readonly Type[] OfficialCardTypes =
    [
        typeof(AdaptiveStrike), typeof(AllForOne), typeof(BallLightning), typeof(Barrage),
        typeof(BeamCell), typeof(BiasedCognition), typeof(BoostAway), typeof(BootSequence),
        typeof(Sts2Sim.Core.Models.Cards.Buffer), typeof(BulkUp), typeof(Capacitor), typeof(Chaos),
        typeof(ChargeBattery), typeof(Chill), typeof(Claw), typeof(ColdSnap),
        typeof(Compact), typeof(CompileDriver), typeof(ConsumingShadow), typeof(Coolant),
        typeof(Coolheaded), typeof(CreativeAi), typeof(Darkness), typeof(DefendDefect),
        typeof(Defragment), typeof(DoubleEnergy), typeof(Dualcast), typeof(EchoForm),
        typeof(EnergySurge), typeof(Feral), typeof(FightThrough), typeof(FlakCannon),
        typeof(FocusedStrike), typeof(Ftl), typeof(Fusion), typeof(GeneticAlgorithm),
        typeof(Glacier), typeof(Glasswork), typeof(GoForTheEyes), typeof(GunkUp),
        typeof(Hailstorm), typeof(HelixDrill), typeof(Hibernate), typeof(Hologram),
        typeof(Hotfix), typeof(Hyperbeam), typeof(IceLance), typeof(Ignition),
        typeof(ImitationLearning), typeof(Iteration), typeof(Leap), typeof(LightningRod),
        typeof(Loop), typeof(MachineLearning), typeof(MeteorStrike), typeof(Modded),
        typeof(MomentumStrike), typeof(MultiCast), typeof(Null), typeof(OneForAll),
        typeof(Overclock), typeof(Quadcast), typeof(Rainbow), typeof(Reboot),
        typeof(Refract), typeof(RocketPunch), typeof(Scavenge), typeof(Scrape),
        typeof(ShadowShield), typeof(Shatter), typeof(SignalBoost), typeof(Skim),
        typeof(Smokestack), typeof(Spinner), typeof(Storm), typeof(StrikeDefect),
        typeof(Subroutine), typeof(Sunder), typeof(Supercritical), typeof(SweepingBeam),
        typeof(Synchronize), typeof(Synthesis), typeof(Tempest), typeof(TeslaCoil),
        typeof(Thunder), typeof(TrashToTreasure), typeof(Turbo), typeof(Uproar),
        typeof(Voltaic), typeof(WhiteNoise), typeof(Zap),
    ];

    public override IReadOnlyList<CardModel> AllCards => OfficialCardTypes
        .Select(type => (CardModel)ModelDb.Get(type)).ToArray();
}
