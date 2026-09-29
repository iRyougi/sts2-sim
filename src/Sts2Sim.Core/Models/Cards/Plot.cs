using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Plot : GeneratedCardModel
{
    public override bool IsMultiplayerOnly => true;

    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 0, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal amount = IsUpgraded ? 3m : 2m;
        foreach (Creature ally in CombatState!.Allies.Where(creature => creature.IsAlive && creature.IsPlayer))
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(CombatState, ally, amount, Owner.Creature, this);
        }
    }
}