using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Monologue : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, CardKeyword.Retain, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await base.OnPlay(cardPlay);
        await PowerCmd.Apply<MonologuePower>(
            CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
