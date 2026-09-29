using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Resonance : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 0, 0m, 0, 0, 0,
        0m, 0m, 1m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null, UpgradeStrength: 1m);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        var combatState = CombatState!;
        decimal amount = Spec.Strength + (IsUpgraded ? Spec.UpgradeStrength : 0m);
        await PowerCmd.Apply<StrengthPower>(combatState, Owner.Creature, amount, Owner.Creature, this);
        foreach (Creature enemy in combatState.HittableEnemies.ToArray())
        {
            await PowerCmd.Apply<StrengthPower>(combatState, enemy, -1m, Owner.Creature, this);
        }
    }
}