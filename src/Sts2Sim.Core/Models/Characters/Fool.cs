using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Characters;

/// <summary>v0.113.1（d09d48e5）Fool 角色定义；专属遗物/药水池按计划 G7 接通。</summary>
public sealed class Fool : CharacterModel
{
    private FoolCardPool? _cardPool;

    public override int StartingHp => 77;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;
    public override int BaseArrowSlotCount => 1;
    public override CardPoolModel CardPool => _cardPool ??= new FoolCardPool();

    public override IReadOnlyList<Type> StartingDeck =>
    [
        typeof(StrikeFool), typeof(StrikeFool), typeof(StrikeFool), typeof(StrikeFool), typeof(StrikeFool),
        typeof(DefendFool), typeof(DefendFool), typeof(DefendFool), typeof(DefendFool),
        typeof(FoolishShot),
    ];

    public override IReadOnlyList<Type> StartingRelics => [typeof(SongBow)];
}
