using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Cruelty : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _bonusPercent = 25m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<CrueltyPower>(CombatState!, Owner.Creature, _bonusPercent, Owner.Creature, this);
    protected override void OnUpgrade() => _bonusPercent += 25m;
}
