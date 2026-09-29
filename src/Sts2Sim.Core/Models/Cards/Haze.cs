using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Haze : CardModel
{
    private decimal Poison => IsUpgraded ? 6m : 4m;
    private decimal Weak => IsUpgraded ? 2m : 1m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay play)
    {
        foreach (Creature enemy in CombatState!.HittableEnemies.ToArray())
        { await PowerCmd.Apply<PoisonPower>(CombatState, enemy, Poison, Owner.Creature, this); await PowerCmd.Apply<WeakPower>(CombatState, enemy, Weak, Owner.Creature, this); }
    }

}
