using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Cascade : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => -1;
    protected override bool IsXEnergyCost => true;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        int count = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        if (IsUpgraded) count++;
        return AutoPlayCmd.FromTopOfDrawPile(CombatState!, Owner, count);
    }
}
