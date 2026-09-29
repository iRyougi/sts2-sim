using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class VoidForm : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        3, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
        false, false, false, new[] { CardKeyword.Ethereal },
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, CardKeyword.Ethereal);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await base.OnPlay(cardPlay);
        (CombatState as CombatState)?.Engine?.RequestEndPlayerTurn();
    }
}
