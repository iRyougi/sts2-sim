using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ToastyMittens : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature)) return;
        CardModel? card = (await CardSelectCmd.FromHand(Owner.Creature.CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this, cancelable: false)).SingleOrDefault();
        if (card is not null)
            await CardPileCmd.Exhaust(Owner.Creature.CombatState!, card);
        await PowerCmd.Apply<StrengthPower>(
            Owner.Creature.CombatState!, Owner.Creature, 1m, Owner.Creature, null);
    }
}
