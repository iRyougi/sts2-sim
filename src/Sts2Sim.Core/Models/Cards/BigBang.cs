using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BigBang : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Rare, TargetType.Self,
        false, false, false, new[] { CardKeyword.Exhaust },
        0m, 0, 0m, 1, 1, 1,
        0m, 0m, 0m, 0m, 5m,
        0m, 0m, 0, 0,
        0m, 0m, false, CardKeyword.Innate, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, Spec.Draw, Owner, fromHandDraw: false);
        await PlayerCmd.GainStars(Spec.GainStars, Owner);
        await PlayerCmd.GainEnergy(Spec.GainEnergy, Owner);
        await ForgeCmd.Forge(Spec.Forge, Owner, this);
    }
}
