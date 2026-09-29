using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Relics;

public sealed class AlchemicalCoffer : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterObtained()
    {
        Owner.GrowPotionSlots(4);
        var generatedIds = new HashSet<ModelId>();
        for (int i = 0; i < 4; i++)
        {
            PotionModel? potion = PotionFactory.CreateRandomOutOfCombat(
                Owner, Owner.RunState.Rng.CombatPotionGeneration,
                candidate => !generatedIds.Contains(candidate.Id));
            if (potion is not null)
            {
                generatedIds.Add(potion.Id);
                await PotionCmd.TryToProcure(potion, Owner);
            }
        }
    }
}
