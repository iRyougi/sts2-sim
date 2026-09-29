using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.PotionPools;

namespace Sts2Sim.Core.Models.Characters;

/// <summary>Regent with the real 10-card starter deck and Divine Right.</summary>
public sealed class Regent : CharacterModel
{
    private static RegentCardPool Pool { get; } = new();
    private static RegentRelicPool RelicPoolInstance { get; } = new();
    private static RegentPotionPool PotionPoolInstance { get; } = new();

    public override int StartingHp => 75;

    public override int StartingGold => 99;

    public override CardPoolModel CardPool => Pool;

    public override RelicPoolModel RelicPool => RelicPoolInstance;

    public override PotionPoolModel PotionPool => PotionPoolInstance;

    public override IReadOnlyList<Type> StartingDeck => new[]
    {
        typeof(StrikeRegent), typeof(StrikeRegent), typeof(StrikeRegent), typeof(StrikeRegent),
        typeof(DefendRegent), typeof(DefendRegent), typeof(DefendRegent), typeof(DefendRegent),
        typeof(FallingStar), typeof(Venerate),
    };

    public override IReadOnlyList<Type> StartingRelics => new[] { typeof(DivineRight) };
}
