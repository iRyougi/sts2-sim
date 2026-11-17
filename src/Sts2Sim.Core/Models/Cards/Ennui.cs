using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>施加 EnnuiPower 并递增其每回合生成 Sadness 张数（原版 <c>Ennui</c>）。</summary>
public sealed class Ennui : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal Amount => IsUpgraded ? 3m : 2m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        EnnuiPower? power = await PowerCmd.Apply<EnnuiPower>(CombatState!, Owner.Creature, Amount, Owner.Creature, this);
        power?.IncrementSadnessAmount();
    }
}
