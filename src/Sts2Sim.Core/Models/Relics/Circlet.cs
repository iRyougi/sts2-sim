using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Fallback relic with no rarity and no gameplay effect.</summary>
public sealed class Circlet : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public override bool IsStackable => true;
}
