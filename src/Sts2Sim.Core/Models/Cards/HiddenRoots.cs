using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>每回合开始获得 Stretch（原版 <c>HiddenRoots</c>）。</summary>
public sealed class HiddenRoots : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal PowerAmount => IsUpgraded ? 4m : 3m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<HiddenRootsPower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
    }
}
