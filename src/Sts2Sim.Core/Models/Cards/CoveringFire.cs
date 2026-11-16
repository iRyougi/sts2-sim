using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>多人专属：射箭时为队友提供格挡（原版 <c>CoveringFire</c>）。</summary>
public sealed class CoveringFire : CardModel, ICardChoiceBaseValueProvider
{
    public override bool IsMultiplayerOnly => true;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal PowerAmount => IsUpgraded ? 11m : 8m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<CoveringFirePower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
    }
}
