using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;
namespace Sts2Sim.Core.Models.Relics;
// v0.114.0 native only checks player == Owner, so event card choices without IsCardReward are enchanted too (#1118).
public sealed class WingCharm : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override bool TryModifyCardRewardOptionsLate(Player p,List<CardModel> o,CardCreationOptions x)=>ReferenceEquals(p,Owner)&&CardFactory.EnchantOneRewardOption<Swift>(p,o,1m); }