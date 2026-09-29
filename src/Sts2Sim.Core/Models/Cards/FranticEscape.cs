using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FranticEscape : CardModel
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.Self;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        SandpitPower? sandpit = CombatState?.Enemies
            .SelectMany(enemy => enemy.Powers.OfType<SandpitPower>())
            .FirstOrDefault(power => power.Target == Owner.Creature);
        if (sandpit is not null)
        {
            await PowerCmd.ModifyAmount(CombatState!, sandpit, 1m, Owner.Creature, this);
        }

        AddEnergyCostThisCombat(1);
    }
}
