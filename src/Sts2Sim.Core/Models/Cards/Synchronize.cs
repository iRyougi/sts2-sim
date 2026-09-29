using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Synchronize : CardModel, ICardChoiceBaseValueProvider
{
    private int _focusPerDistinctOrb = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        int distinctOrbs = Owner.PlayerCombatState!.OrbQueue.Orbs.Select(orb => orb.Id).Distinct().Count();
        return PowerCmd.Apply<SynchronizePower>(CombatState!, Owner.Creature,
            distinctOrbs * _focusPerDistinctOrb, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _focusPerDistinctOrb += 1;
}
