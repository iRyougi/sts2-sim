using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Tutor : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, true, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.Target.Player is not { } targetPlayer)
        {
            throw new InvalidOperationException("Tutor requires a player target.");
        }

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromCombatPile(
            CombatState!,
            targetPlayer,
            targetPlayer.PlayerCombatState!.DrawPile,
            1,
            1,
            this);
        foreach (CardModel card in selected)
        {
            CardPileCmd.Add(card, PileType.Hand);
        }
    }
}
