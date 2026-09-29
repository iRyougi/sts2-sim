using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BurningSticks : RelicModel
{
    private bool _wasUsedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _wasUsedThisCombat = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        if (_wasUsedThisCombat || card.Owner != Owner || card.Type != CardType.Skill)
        {
            return;
        }

        CardModel copy = (CardModel)card.MutableClone();
        await CardPileCmd.Generate(card.CombatState!, copy, PileType.Hand);
        _wasUsedThisCombat = true;
    }

    public override Task AfterCombatEnd()
    {
        _wasUsedThisCombat = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_wasUsedThisCombat);
    }
}
