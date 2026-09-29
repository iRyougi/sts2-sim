using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DyingStar : GeneratedCardModel
{
    private const decimal StrengthLoss = 9m;
    private const decimal UpgradeStrengthLoss = 2m;

    protected override GeneratedCardSpec Spec { get; } = new(
        1, 3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies,
        false, false, false, new[] { CardKeyword.Ethereal },
        9m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        Creature[] enemies = CombatState!.HittableEnemies.ToArray();
        await base.OnPlay(cardPlay);

        decimal strengthLoss = StrengthLoss + (IsUpgraded ? UpgradeStrengthLoss : 0m);
        foreach (Creature enemy in enemies)
        {
            await PowerCmd.Apply<DyingStarPower>(CombatState!, enemy, strengthLoss, Owner.Creature, this);
        }
    }
}
