using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BrightestFlame : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int amount = IsUpgraded ? 3 : 2;
        Owner.PlayerCombatState!.GainEnergy(amount);
        await CardPileCmd.Draw(CombatState!, amount, Owner, fromHandDraw: false);
        await CreatureCmd.LoseMaxHp(Owner.RunState, Owner.Creature, 2m, isFromCard: true);
    }
}
