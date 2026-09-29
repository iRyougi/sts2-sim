using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BelieveInYou : GeneratedCardModel
{
    public override bool IsMultiplayerOnly => true;

    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 2, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null, UpgradeGainEnergy: 1);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        Creature target = cardPlay.Target
            ?? throw new ArgumentNullException(nameof(cardPlay.Target));
        Player targetPlayer = target.Player
            ?? throw new InvalidOperationException("BelieveInYou requires a player target.");
        int amount = Spec.GainEnergy + (IsUpgraded ? Spec.UpgradeGainEnergy : 0);
        await PlayerCmd.GainEnergy(amount, targetPlayer);
    }
}