using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Vetoes the owner's first-turn hand flush.</summary>
public sealed class RingingTriangle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool ShouldFlush(Entities.Players.Player player) =>
        player != Owner || Owner.PlayerCombatState!.TurnNumber > 1;
}
