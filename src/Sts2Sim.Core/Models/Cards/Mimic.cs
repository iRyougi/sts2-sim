using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Mimic : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly,
        true, false, false, new[] { CardKeyword.Exhaust },
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, CardKeyword.Exhaust);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.GainBlock(
            CombatState!,
            Owner.Creature,
            cardPlay.Target.Block,
            ValueProp.Move,
            this,
            cardPlay);
    }
}
