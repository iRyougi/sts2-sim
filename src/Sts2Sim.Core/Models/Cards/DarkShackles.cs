using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DarkShackles : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, new[] { CardKeyword.Exhaust },
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await base.OnPlay(cardPlay);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal strengthLoss = IsUpgraded ? 15m : 9m;
        await PowerCmd.Apply<DarkShacklesPower>(
            CombatState!,
            cardPlay.Target,
            strengthLoss,
            Owner.Creature,
            this);
    }
}
