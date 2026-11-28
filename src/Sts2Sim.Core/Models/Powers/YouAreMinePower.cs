using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class YouAreMinePower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player == Owner.Player && player.PlayerCombatState is { TurnNumber: >= 2 })
        {
            await PowerCmd.Apply<WeakPower>(Owner.CombatState!, player.Creature, Amount, applier: null, cardSource: null);
            await PowerCmd.Apply<VulnerablePower>(Owner.CombatState!, player.Creature, Amount, applier: null, cardSource: null);
            await PowerCmd.Apply<FrailPower>(Owner.CombatState!, player.Creature, Amount, applier: null, cardSource: null);
            await PowerCmd.Remove(this);
        }
    }
}
