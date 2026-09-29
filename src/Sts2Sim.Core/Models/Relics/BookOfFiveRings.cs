using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BookOfFiveRings : RelicModel
{
    private int _cardsAdded;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        _ = clonedBy;
        if (Owner.Creature.IsDead ||
            card.Owner != Owner ||
            card.Pile?.Type != PileType.Deck ||
            oldPileType == PileType.Deck)
        {
            return;
        }

        _cardsAdded++;
        if (_cardsAdded < 5)
        {
            return;
        }

        _cardsAdded -= 5;
        await CreatureCmd.Heal(Owner.Creature, 20m);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_cardsAdded);
    }
}
