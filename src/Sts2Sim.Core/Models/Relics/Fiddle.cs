using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Fiddle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyHandDraw(Player player, decimal amount) => player == Owner ? amount + 2m : amount;
    public override bool ShouldDraw(Player player, bool fromHandDraw) =>
        fromHandDraw || player != Owner || player.Creature.CombatState?.CurrentSide != player.Creature.Side;
}
