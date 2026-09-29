using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Astrolabe : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        IReadOnlyList<CardModel> selected =
            await CardSelectCmd.FromDeckForTransformation(Owner, 3, this);
        foreach (CardModel original in selected)
        {
            CardModel replacement = CardFactory.CreateRandomCardForTransform(original, false, Owner.RunState.Rng.Niche);
            CardCmd.Upgrade(replacement);
            await CardCmd.Transform(original, replacement);
        }
    }
}
