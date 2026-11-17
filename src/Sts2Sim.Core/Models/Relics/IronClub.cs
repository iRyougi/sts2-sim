using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class IronClub : RelicModel
{
    private const int CardsThreshold = 4; // CardsVar(4)

    private int _cardsPlayed;
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() =>
        Status = _cardsPlayed % CardsThreshold == CardsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner) return;
        _cardsPlayed++;
        // IronClub.UpdateDisplay via the CardsPlayed setter (IsActivating is animation only). The source counter
        // never wraps; this one resets to 0 on a trigger, so compare the same modulo the state description uses.
        Status = _cardsPlayed % CardsThreshold == CardsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        if (_cardsPlayed < 4 || Owner.Creature.CombatState is null) return;
        _cardsPlayed = 0;
        if (Owner.Creature.CombatState is not null)
            await CardPileCmd.Draw(Owner.Creature.CombatState, 1, Owner, fromHandDraw: false);
    }
    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_cardsPlayed % 4);
}
