using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Scavenge : CardModel, ICardChoiceBaseValueProvider
{
    private int _energy = 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: _energy);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null) await CardPileCmd.Exhaust(CombatState!, selected);
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature,
            _energy, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _energy += 1;
}
