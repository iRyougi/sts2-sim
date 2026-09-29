using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Shroud</c>：1 费能力，获得 3（升级 4）层 <see cref="ShroudPower"/>。</summary>
public sealed class Shroud : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    // 原版 BlockVar(3, Unpowered) 是能力层数，键名仍是字面 Block。
    private decimal Block => IsUpgraded ? 4m : 3m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<ShroudPower>(CombatState!, Owner.Creature, Block, Owner.Creature, this);
}
