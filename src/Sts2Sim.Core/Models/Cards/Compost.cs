using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>仅多人牌：施加 CompostPower（原版 <c>Compost</c>）。</summary>
public sealed class Compost : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 0;
    private int Energy => IsUpgraded ? 2 : 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<CompostPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
    }
}
