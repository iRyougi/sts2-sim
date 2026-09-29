using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class GuidingStar : GeneratedCardModel
{
    // v0.111.0 正式版：星费 1；造成伤害后给自己施加 DrawCardsNextTurnPower（2，升级 3），不是当场抽牌。
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        12m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        1m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await base.OnPlay(cardPlay);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(
            CombatState!,
            Owner.Creature,
            IsUpgraded ? 3m : 2m,
            Owner.Creature,
            this);
    }
}
