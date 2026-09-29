using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CrimsonMantle : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _block = 7m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CrimsonMantlePower? power = await PowerCmd.Apply<CrimsonMantlePower>(
            CombatState!, Owner.Creature, _block, Owner.Creature, this);
        power?.IncrementSelfDamage();
    }

    protected override void OnUpgrade() => _block += 3m;
}
