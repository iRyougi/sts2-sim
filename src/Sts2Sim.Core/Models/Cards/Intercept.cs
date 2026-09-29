using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Intercept : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 9m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 4m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        Creature target = cardPlay.Target
            ?? throw new ArgumentNullException(nameof(cardPlay.Target));
        decimal block = Spec.Block + (IsUpgraded ? Spec.UpgradeBlock : 0m);
        await CreatureCmd.GainBlock(
            CombatState!,
            Owner.Creature,
            block,
            ValueProp.Move,
            this,
            cardPlay);
        await PowerCmd.Apply<CoveredPower>(
            CombatState!,
            target,
            1m,
            Owner.Creature,
            this);
    }
}
