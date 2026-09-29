using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class CallingBell : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var curse = (CurseOfTheBell)ModelDb.Card<CurseOfTheBell>().MutableClone();
        curse.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(curse);
        await Cmd.Wait(0.75f);
        // #328: omit upstream TestMode's fixed Anchor/GremlinHorn/MummifiedHand rewards.
        await RewardsCmd.OfferCustom(Owner,
        [
            new RelicReward(RelicRarity.Common, Owner),
            new RelicReward(RelicRarity.Uncommon, Owner),
            new RelicReward(RelicRarity.Rare, Owner),
        ]);
    }
}
