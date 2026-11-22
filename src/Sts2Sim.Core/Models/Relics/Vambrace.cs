using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Vambrace : RelicModel
{
    // game Vambrace: the first card to gain block is remembered (TriggeringCard), all block from that card's play doubles,
    // and BlockGainedThisCombat is set only when that card's play finishes (AfterCardPlayed).
    private CardModel? _triggeringCard;
    private bool _blockGainedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _triggeringCard = null;
        _blockGainedThisCombat = false;
        Status = RelicStatus.Active;
        return Task.CompletedTask;
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!props.HasFlag(ValueProp.Move) ||
            cardSource is null ||
            (_triggeringCard is not null && !ReferenceEquals(_triggeringCard, cardSource)) ||
            cardSource.Owner != Owner ||
            _blockGainedThisCombat)
        {
            return 1m;
        }

        return 2m;
    }

    // Source Vambrace.AfterModifyingBlockAmount. Hook.AfterModifyingBlockAmount only reaches modifiers recorded by
    // Hook.ModifyBlock (factor != 1), like the source; previews that only call ModifyBlock never reach it.
    public override Task AfterModifyingBlockAmount(
        decimal modifiedAmount,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (modifiedAmount > 0m && cardSource is not null)
        {
            Status = RelicStatus.Normal;
            _triggeringCard = cardSource;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner &&
            ReferenceEquals(cardPlay.Card, _triggeringCard) &&
            !_blockGainedThisCombat)
        {
            _blockGainedThisCombat = true;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        _triggeringCard = null;
        _blockGainedThisCombat = false;
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        var sourceRelic = (Vambrace)source;
        if (sourceRelic._triggeringCard is not { } sourceCard)
        {
            _triggeringCard = null;
            return;
        }

        if (cardMap.TryGetValue(sourceCard, out CardModel? mappedCard))
        {
            _triggeringCard = mappedCard;
            return;
        }

        if (sourceRelic._blockGainedThisCombat)
        {
            _triggeringCard = null;
            return;
        }

        throw new InvalidOperationException(
            "Vambrace cannot clone an active triggering card that is absent from the combat card graph.");
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_blockGainedThisCombat);
        context.AppendCardReferences(ref builder, new CardModel?[] { _triggeringCard });
    }
}
