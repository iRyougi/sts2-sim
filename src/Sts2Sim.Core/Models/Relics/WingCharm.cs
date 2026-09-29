using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;
namespace Sts2Sim.Core.Models.Relics;
public sealed class WingCharm : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override bool TryModifyCardRewardOptionsLate(Player p,List<CardModel> o,CardCreationOptions x)=>ReferenceEquals(p,Owner)&&x.Flags.HasFlag(CardCreationFlags.IsCardReward)&&CardFactory.EnchantOneRewardOption<Swift>(p,o,1m); }