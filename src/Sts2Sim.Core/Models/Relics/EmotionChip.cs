using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class EmotionChip : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (Owner.Creature.CombatState?.IsLiveCombat() == true &&
            target == Owner.Creature && result.UnblockedDamage > 0)
            Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner) return;
        CombatState state = (CombatState)Owner.Creature.CombatState!;
        bool lostHpInPreviousTurn = state.DamageHistory.Entries.Any(entry =>
            entry.Receiver == Owner.Creature &&
            !entry.Result.WasFullyBlocked &&
            entry.HappenedLastPlayerTurn(Owner));
        if (!lostHpInPreviousTurn) return;

        Flash();
        foreach (OrbModel orb in Owner.PlayerCombatState!.OrbQueue.Orbs)
            await OrbCmd.Passive(state, orb, countAffectedByHooks: true);
    }
}
