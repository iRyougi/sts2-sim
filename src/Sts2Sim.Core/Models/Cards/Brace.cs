using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>X 费施加 X 次 AimPower（原版 <c>Brace</c>）。</summary>
public sealed class Brace : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
    private decimal AimAmount => IsUpgraded ? 11m : 8m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int xValue = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        for (int i = 0; i < xValue; i++)
        {
            await PowerCmd.Apply<AimPower>(CombatState!, Owner.Creature, AimAmount, Owner.Creature, this);
        }
    }
}
