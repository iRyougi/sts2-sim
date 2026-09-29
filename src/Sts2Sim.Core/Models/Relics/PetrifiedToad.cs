using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Potions;
namespace Sts2Sim.Core.Models.Relics;
public sealed class PetrifiedToad : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override async Task BeforeCombatStartLate() { Flash(); await PotionCmd.TryToProcure((PotionModel)ModelDb.Potion<PotionShapedRock>().MutableClone(), Owner); }
}
