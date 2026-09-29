using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class NewLeaf : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        CardModel? selected = (await CardSelectCmd.FromDeckForTransformation(Owner, 1, this))
            .FirstOrDefault();
        if (selected is not null)
        {
            await CardCmd.TransformToRandom(selected, Owner.RunState.Rng.Niche, Owner.RunState);
        }
    }
}
