using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Characters;

/// <summary>Necrobinder's v0.111.0 starting state and character-owned content pools.</summary>
public sealed class Necrobinder : CharacterModel
{
    private NecrobinderCardPool? _cardPool;
    private NecrobinderRelicPool? _relicPool;
    private NecrobinderPotionPool? _potionPool;

    public override int StartingHp => 66;

    public override int StartingGold => 99;

    public override int MaxEnergy => 3;

    public override CardPoolModel CardPool => _cardPool ??= new NecrobinderCardPool();

    public override RelicPoolModel RelicPool => _relicPool ??= new NecrobinderRelicPool();

    public override PotionPoolModel PotionPool => _potionPool ??= new NecrobinderPotionPool();

    public override IReadOnlyList<Type> StartingDeck =>
    [
        typeof(StrikeNecrobinder), typeof(StrikeNecrobinder), typeof(StrikeNecrobinder), typeof(StrikeNecrobinder),
        typeof(DefendNecrobinder), typeof(DefendNecrobinder), typeof(DefendNecrobinder), typeof(DefendNecrobinder),
        typeof(Bodyguard), typeof(Unleash),
    ];

    public override IReadOnlyList<Type> StartingRelics => [typeof(BoundPhylactery)];
}
