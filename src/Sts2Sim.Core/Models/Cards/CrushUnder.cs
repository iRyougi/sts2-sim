using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CrushUnder : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Common, TargetType.AllEnemies,
        false, false, false, Array.Empty<CardKeyword>(),
        8m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        1m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<Creature> enemies = CombatState!.HittableEnemies;
        await base.OnPlay(cardPlay);
        decimal strengthLoss = IsUpgraded ? 2m : 1m;
        foreach (Creature enemy in enemies)
        {
            await PowerCmd.Apply<CrushUnderPower>(
                CombatState,
                enemy,
                strengthLoss,
                Owner.Creature,
                this);
        }
    }
}
