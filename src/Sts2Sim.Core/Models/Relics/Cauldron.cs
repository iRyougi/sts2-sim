using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
namespace Sts2Sim.Core.Models.Relics;
public sealed class Cauldron : RelicModel { public override RelicRarity Rarity => RelicRarity.Shop; public override bool HasUponPickupEffect => true; public override Task AfterObtained() => RewardsCmd.OfferCustom(Owner, Enumerable.Range(0,5).Select(_ => (Reward)new PotionReward(Owner)).ToList()); }