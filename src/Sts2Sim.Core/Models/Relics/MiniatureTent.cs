using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
namespace Sts2Sim.Core.Models.Relics;
public sealed class MiniatureTent : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override bool ShouldDisableRemainingRestSiteOptions(Player p){if(!ReferenceEquals(p,Owner))return true;Flash();return false;} }