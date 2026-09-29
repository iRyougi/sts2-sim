using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HelicalDart : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || !cardPlay.Card.Tags.Contains(CardTag.Shiv))
        {
            return;
        }

        await PowerCmd.Apply<HelicalDartPower>(
            Owner.Creature.CombatState!, Owner.Creature, 1m, Owner.Creature, null);
    }
}
