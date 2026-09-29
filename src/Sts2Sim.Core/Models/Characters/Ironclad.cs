using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Characters;

/// <summary>Ironclad's v0.111.0 starting state and character-owned content pools.</summary>
public sealed class Ironclad : CharacterModel
{
    private IroncladCardPool? _cardPool;
    private IroncladRelicPool? _relicPool;
    private IroncladPotionPool? _potionPool;

    public override int StartingHp => 80;

    public override int StartingGold => 99;

    public override int MaxEnergy => 3;

    public override CardPoolModel CardPool => _cardPool ??= new IroncladCardPool();

    public override RelicPoolModel RelicPool => _relicPool ??= new IroncladRelicPool();

    public override PotionPoolModel PotionPool => _potionPool ??= new IroncladPotionPool();

    public override IReadOnlyList<Type> StartingDeck =>
    [
        typeof(StrikeIronclad), typeof(StrikeIronclad), typeof(StrikeIronclad),
        typeof(StrikeIronclad), typeof(StrikeIronclad),
        typeof(DefendIronclad), typeof(DefendIronclad),
        typeof(DefendIronclad), typeof(DefendIronclad),
        typeof(Bash),
    ];

    public override IReadOnlyList<Type> StartingRelics => [typeof(BurningBlood)];
}
