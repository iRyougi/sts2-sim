using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Juggernaut : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    private decimal _damage = 6m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<JuggernautPower>(CombatState!, Owner.Creature, _damage, Owner.Creature, this);
    protected override void OnUpgrade() => _damage += 2m;
}
