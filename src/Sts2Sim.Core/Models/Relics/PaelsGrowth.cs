using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsGrowth : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override void ModifyAvailableRestSiteDecisions(IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (ReferenceEquals(player, Owner))
        {
            decisions.Add(new CloneRestSiteOption());
        }
    }

    public override async Task AfterObtained()
    {
        Clone clone = ModelDb.GetById<Clone>(ModelDb.GetId<Clone>());
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(Owner.RunState, Owner,
            Owner.Deck.Cards.Where(clone.CanEnchant).ToList(), 1, 1, this)).FirstOrDefault();
        if (selected is not null)
        {
            await CardCmd.Enchant<Clone>(selected, 4m);
        }
    }
}
