using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
namespace Sts2Sim.Core.Models.Relics;
public sealed class PunchDagger : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override bool HasUponPickupEffect=>true; public override async Task AfterObtained(){var e=(Momentum)ModelDb.Get(typeof(Momentum));foreach(var c in await CardSelectCmd.SelectCardsAsync(Owner.RunState,Owner,Owner.Deck.Cards.Where(e.CanEnchant),1,1,this))await CardCmd.Enchant<Momentum>(c,5m);} }