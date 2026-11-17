using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class JossPaper : RelicModel
{
    private const int ExhaustsPerDraw = 5;

    private int _cardsExhausted;
    private int _etherealCount;

    // Source CardsExhausted setter: Status = CardsExhausted == ExhaustAmount - 1 ? Active : Normal
    // (IsActivating / DoActivateVisuals is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = _cardsExhausted == ExhaustsPerDraw - 1 ? RelicStatus.Active : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() => UpdateStatus();

    public override async Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner)
        {
            return;
        }

        if (causedByEthereal)
        {
            _etherealCount++;
            return;
        }

        _cardsExhausted++;
        UpdateStatus();
        await DrawIfThresholdMet();
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        _cardsExhausted += _etherealCount;
        UpdateStatus();
        _etherealCount = 0;
        await DrawIfThresholdMet();
    }

    public override Task AfterCombatEnd()
    {
        _etherealCount = 0;
        return Task.CompletedTask;
    }

    private async Task DrawIfThresholdMet()
    {
        if (_cardsExhausted < ExhaustsPerDraw)
        {
            return;
        }

        int drawCount = _cardsExhausted / ExhaustsPerDraw;
        _cardsExhausted %= ExhaustsPerDraw;
        UpdateStatus();
        await CardPileCmd.Draw(
            Owner.Creature.CombatState!,
            drawCount,
            Owner,
            fromHandDraw: false);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_cardsExhausted);
        builder.Append(_etherealCount);
    }
}
