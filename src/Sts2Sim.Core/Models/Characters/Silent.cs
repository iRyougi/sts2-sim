using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Characters;

/// <summary>Silent character skeleton with the official starter stats, deck, and relic.</summary>
public sealed class Silent : CharacterModel
{
    private SilentCardPool? _cardPool;
    private SilentRelicPool? _relicPool;
    private SilentPotionPool? _potionPool;

    public override int StartingHp => 70;

    public override int StartingGold => 99;

    public override int MaxEnergy => 3;

    public override CardPoolModel CardPool => _cardPool ??= new SilentCardPool();

    public override RelicPoolModel RelicPool => _relicPool ??= new SilentRelicPool();

    public override PotionPoolModel PotionPool => _potionPool ??= new SilentPotionPool();

    public override IReadOnlyList<Type> StartingDeck => new[]
    {
        typeof(StrikeSilent), typeof(StrikeSilent), typeof(StrikeSilent),
        typeof(StrikeSilent), typeof(StrikeSilent),
        typeof(DefendSilent), typeof(DefendSilent), typeof(DefendSilent),
        typeof(DefendSilent), typeof(DefendSilent),
        typeof(Neutralize), typeof(Survivor),
    };

    public override IReadOnlyList<Type> StartingRelics => new[] { typeof(RingOfTheSnake) };
}
