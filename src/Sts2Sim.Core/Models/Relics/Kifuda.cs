using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
namespace Sts2Sim.Core.Models.Relics;
public sealed class Kifuda : RelicModel { public override RelicRarity Rarity=>RelicRarity.Shop; public override bool HasUponPickupEffect=>true; public override async Task AfterObtained(){var e=(Adroit)ModelDb.Get(typeof(Adroit)); foreach(var c in await CardSelectCmd.SelectCardsAsync(Owner.RunState,Owner,Owner.Deck.Cards.Where(e.CanEnchant),0,3,this)) await CardCmd.Enchant<Adroit>(c,3m);} }