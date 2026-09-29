using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Outbreak : CardModel
{
    private decimal Poison => IsUpgraded ? 12m : 9m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 3;
    protected override async Task OnPlay(CardPlay play)
    {
        Creature[] enemies = CombatState!.HittableEnemies.ToArray();
        foreach (Creature enemy in enemies) await PowerCmd.Apply<PoisonPower>(CombatState, enemy, Poison, Owner.Creature, this);
        foreach (Creature enemy in enemies)
            if (enemy.GetPower<PoisonPower>() is { } poison) await poison.Trigger();
    }

}
