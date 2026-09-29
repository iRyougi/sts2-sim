using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Characters;

/// <summary>Defect starting setup from v0.111.0.</summary>
public sealed class Defect : CharacterModel
{
    private static DefectCardPool CardPoolInstance { get; } = new();
    private static DefectRelicPool RelicPoolInstance { get; } = new();
    private static DefectPotionPool PotionPoolInstance { get; } = new();

    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;
    public override int BaseOrbSlotCount => 3;
    public override CardPoolModel CardPool => CardPoolInstance;
    public override RelicPoolModel RelicPool => RelicPoolInstance;
    public override PotionPoolModel PotionPool => PotionPoolInstance;

    public override IReadOnlyList<Type> StartingDeck =>
    [
        typeof(StrikeDefect), typeof(StrikeDefect), typeof(StrikeDefect), typeof(StrikeDefect),
        typeof(DefendDefect), typeof(DefendDefect), typeof(DefendDefect), typeof(DefendDefect),
        typeof(Zap), typeof(Dualcast),
    ];

    public override IReadOnlyList<Type> StartingRelics => [typeof(CrackedCore)];
}
