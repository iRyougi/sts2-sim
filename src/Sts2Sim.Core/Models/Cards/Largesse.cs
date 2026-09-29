using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Largesse : GeneratedCardModel
{
    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
        false, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.Target.Player is not { } targetPlayer)
        {
            throw new InvalidOperationException("Largesse requires a player target.");
        }

        CardModel? generated = CardFactory.GetDistinctForCombat(
            targetPlayer,
            ColorlessCardPool.GetUnlockedCards(targetPlayer),
            1,
            Owner.RunState.Rng.CombatCardGeneration).SingleOrDefault();
        if (generated is null)
        {
            return;
        }

        if (IsUpgraded)
        {
            generated.Upgrade();
        }

        await CardPileCmd.Generate(CombatState!, generated, PileType.Hand, Owner);
    }
}
