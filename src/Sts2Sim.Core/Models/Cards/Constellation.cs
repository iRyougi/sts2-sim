using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Constellation : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 9m, 1, 1, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 3m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        Creature target = cardPlay.Target
            ?? throw new ArgumentNullException(nameof(cardPlay.Target));
        Player targetPlayer = target.Player
            ?? throw new InvalidOperationException("Constellation requires a player target.");
        await CardPileCmd.Draw(CombatState!, Spec.Draw, targetPlayer, fromHandDraw: false);
        await PlayerCmd.GainEnergy(Spec.GainEnergy, targetPlayer);
        decimal block = Spec.Block + (IsUpgraded ? Spec.UpgradeBlock : 0m);
        await CreatureCmd.GainBlock(
            CombatState!,
            target,
            block,
            Sts2Sim.Core.ValueProps.ValueProp.Move,
            this,
            cardPlay);
    }
}
