using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsEye : RelicModel
{
    private bool _usedThisCombat;
    private bool _wasOwnerPartOfLastPlayerTurn = true;
    private bool _eligibleForExtraTurn;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            _wasOwnerPartOfLastPlayerTurn = participants.Contains(Owner.Creature);
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEndEarly(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        _eligibleForExtraTurn =
            !_usedThisCombat &&
            _wasOwnerPartOfLastPlayerTurn &&
            side == CombatSide.Player &&
            participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState?.ManualCardsPlayedThisTurn == 0;
        if (!_eligibleForExtraTurn)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards.ToList())
        {
            await CardPileCmd.Exhaust(combatState, card);
        }
    }

    public override bool ShouldTakeExtraTurn(Player player) =>
        player == Owner && !_usedThisCombat && _eligibleForExtraTurn;

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner)
        {
            _usedThisCombat = true;
            _eligibleForExtraTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _usedThisCombat = false;
        _wasOwnerPartOfLastPlayerTurn = true;
        _eligibleForExtraTurn = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_usedThisCombat);
        builder.Append(_wasOwnerPartOfLastPlayerTurn);
        builder.Append(_eligibleForExtraTurn);
    }
}
