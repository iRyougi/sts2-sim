using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DelicateFrond : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task BeforeCombatStart()
    {
        while (Owner.PotionSlots.Contains(null))
        {
            var potion = PotionFactory.CreateRandomOutOfCombat(Owner, Owner.RunState.Rng.CombatPotionGeneration);
            if (potion is null || !await PotionCmd.TryToProcure(potion, Owner))
            {
                break;
            }
        }
    }
}
