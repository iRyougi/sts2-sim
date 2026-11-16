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
            // v0.113.1：变形改用 PlayerRng.Transformations（原 RunState.Rng.Niche）。
            await CardCmd.TransformToRandom(selected, Owner.PlayerRng.Transformations, Owner.RunState);
        }
    }
}
