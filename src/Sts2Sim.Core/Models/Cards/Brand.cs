using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Brand : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _strength = 1m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.Damage(CombatState!, [Owner.Creature], 1m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature, this, cardPlay);
        CardModel? selected = (await CardSelectCmd.FromHand(
            CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards, 1, 1, this)).FirstOrDefault();
        if (selected is not null) await CardPileCmd.Exhaust(CombatState!, selected);
        await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, _strength, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _strength++;
}
