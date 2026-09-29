using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Relax : CardModel
{
    public override bool GainsBlock => true;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal amount = IsUpgraded ? 3m : 2m;
        await CreatureCmd.GainBlock(
            CombatState!, Owner.Creature, IsUpgraded ? 18m : 16m, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(
            CombatState!, Owner.Creature, amount, Owner.Creature, this);
        await PowerCmd.Apply<EnergyNextTurnPower>(
            CombatState!, Owner.Creature, amount, Owner.Creature, this);
    }
}
