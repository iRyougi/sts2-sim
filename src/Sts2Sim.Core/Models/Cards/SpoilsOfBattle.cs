using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SpoilsOfBattle : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Common, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 2, 0, 0,
        0m, 0m, 0m, 0m, 6m,
        0m, 0m, 0, 0,
        0m, 3m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal forge = Spec.Forge + (IsUpgraded ? Spec.UpgradeForge : 0m);
        await ForgeCmd.Forge(forge, Owner, this);
        await CardPileCmd.Draw(CombatState!, Spec.Draw, Owner, fromHandDraw: false);
    }
}
