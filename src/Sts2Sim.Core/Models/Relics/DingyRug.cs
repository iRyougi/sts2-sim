using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Runs;
namespace Sts2Sim.Core.Models.Relics;
public sealed class DingyRug : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override CardCreationOptions ModifyCardRewardCreationOptions(Player p, CardCreationOptions o) => ReferenceEquals(Owner,p) && o.Flags.HasFlag(CardCreationFlags.IsCardReward) && !o.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications) ? o.WithCardPools(o.CardPools.Union([ColorlessCardPool.Instance])) : o; }