using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ThrowingAxe : RelicModel
{
    private bool _used;
    public bool UsedThisCombat => _used;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterRoomEntered(AbstractRoom room) { _used = false; return Task.CompletedTask; }
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) => !_used && card.Owner == Owner ? playCount + 1 : playCount;
    public override Task AfterModifyingCardPlayCount(CardModel card) { if (card.Owner == Owner) _used = true; return Task.CompletedTask; }
    public override Task AfterCombatEnd() { _used = false; return Task.CompletedTask; }
    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_used);
}
