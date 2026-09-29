using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Discards after the owner draws, in the normal player-start phase.</summary>
public sealed class ToolsOfTheTradePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) => player == Owner.Player ? originalCardCount + Amount : originalCardCount;
    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player) return;
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHandForDiscard(Owner.CombatState!, Owner.Player!, Amount, this);
        await CardCmd.Discard(selected);
    }
}
