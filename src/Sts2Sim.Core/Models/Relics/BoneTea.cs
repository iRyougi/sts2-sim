using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BoneTea : RelicModel
{
    private int _combatsLeft = 1;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsUsedUp => _combatsLeft <= 0;

    public int CombatsLeft => Math.Max(0, _combatsLeft);

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (IsUsedUp ||
            !participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in Owner.PlayerCombatState.Hand.Cards.Where(card => card.IsUpgradable))
        {
            CardCmd.Upgrade(card);
        }

        _combatsLeft--;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_combatsLeft);
    }
}
