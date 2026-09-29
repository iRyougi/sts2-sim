using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Rally : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        2, 0, CardType.Skill, CardRarity.Rare, TargetType.AllAllies,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 12m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 5m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal block = Spec.Block + (IsUpgraded ? Spec.UpgradeBlock : 0m);
        foreach (Creature ally in CombatState!.Allies.Where(creature => creature.IsAlive && creature.IsPlayer))
        {
            await CreatureCmd.GainBlock(
                CombatState,
                ally,
                block,
                ValueProp.Move,
                this,
                cardPlay);
        }
    }
}
