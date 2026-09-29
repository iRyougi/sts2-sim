using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
namespace Sts2Sim.Core.Models.Relics;
public sealed class Orrery : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override bool HasUponPickupEffect=>true; public override Task AfterObtained()=>RewardsCmd.OfferCustom(Owner,Enumerable.Range(0,5).Select(_=>(Reward)new CardReward(Owner,new CardCreationOptions([Owner.Character.CardPool],CardCreationSource.Other,CardRarityOddsType.RegularEncounter),3)).ToList()); }