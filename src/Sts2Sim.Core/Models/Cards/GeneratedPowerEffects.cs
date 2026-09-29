using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

internal static class GeneratedPowerEffects
{
    internal sealed record Effect(Type PowerType, decimal Amount, decimal UpgradeAmount);
    private static readonly IReadOnlyDictionary<Type, Effect> Effects =
        new Dictionary<Type, Effect>
        {
            [typeof(Conqueror)] = new(typeof(ConquerorPower), 1m, 0m),
            [typeof(Furnace)] = new(typeof(FurnacePower), 5m, 2m),
            [typeof(PaleBlueDot)] = new(typeof(PaleBlueDotPower), 1m, 1m),
            [typeof(Parry)] = new(typeof(ParryPower), 10m, 4m),
            [typeof(Reflect)] = new(typeof(ReflectPower), 1m, 0m),
            [typeof(SpectrumShift)] = new(typeof(SpectrumShiftPower), 1m, 0m),
            [typeof(Arsenal)] = new(typeof(ArsenalPower), 1m, 0m),
            [typeof(ForegoneConclusion)] = new(typeof(ForegoneConclusionPower), 2m, 1m),
            [typeof(HammerTime)] = new(typeof(HammerTimePower), 1m, 0m),
            [typeof(MonarchsGaze)] = new(typeof(MonarchsGazePower), 1m, 0m),
            [typeof(NeutronAegis)] = new(typeof(PlatingPower), 8m, 3m),
            [typeof(Royalties)] = new(typeof(RoyaltiesPower), 30m, 10m),
            [typeof(SeekingEdge)] = new(typeof(SeekingEdgePower), 1m, 4m),
            [typeof(SwordSage)] = new(typeof(SwordSagePower), 1m, 0m),
            [typeof(Tyranny)] = new(typeof(TyrannyPower), 1m, 0m),
            [typeof(VoidForm)] = new(typeof(VoidFormPower), 2m, 0m),
            [typeof(TheSealedThrone)] = new(typeof(TheSealedThronePower), 1m, 0m),
            [typeof(Stratagem)] = new(typeof(StratagemPower), 1m, 0m),
            [typeof(TagTeam)] = new(typeof(TagTeamPower), 1m, 0m),
            [typeof(BeaconOfHope)] = new(typeof(BeaconOfHopePower), 1m, 0m),
            [typeof(Calamity)] = new(typeof(CalamityPower), 1m, 0m),
            [typeof(Entropy)] = new(typeof(EntropyPower), 1m, 0m),
            [typeof(EternalArmor)] = new(typeof(PlatingPower), 9m, 3m),
            [typeof(Knockdown)] = new(typeof(KnockdownPower), 2m, 1m),
            [typeof(Mayhem)] = new(typeof(MayhemPower), 1m, 0m),
            [typeof(Nostalgia)] = new(typeof(NostalgiaPower), 1m, 0m),
            [typeof(TheGambit)] = new(typeof(TheGambitPower), 1m, 0m),
        };

    public static bool TryGet(Type cardType, out Effect effect) => Effects.TryGetValue(cardType, out effect!);
}
