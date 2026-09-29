using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class IntimidatingHelmet : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Resources.EnergySpent < 2)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            4m,
            ValueProp.Unpowered,
            null,
            cardPlay);
    }
}
