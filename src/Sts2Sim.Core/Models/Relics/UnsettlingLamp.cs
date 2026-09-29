using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class UnsettlingLamp : RelicModel
{
    private CardModel? _triggeringCard;
    private bool _finished;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeCombatStart()
    {
        Reset();
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        CardModel? cardSource)
    {
        // 偏离 #130：the simulator still has no temporary-power marker (ITemporaryPower) or power
        // visibility surface (power.IsVisible), so those two upstream exclusions remain unrepresented.
        // 偏离 #309（2026-09-08）：ArtifactPower 现已存在，恢复权威的 target.HasPower<ArtifactPower>() 排除
        // ——被 Artifact 挡掉的 debuff 不该让本遗物进入触发态。
        if (!_finished &&
            _triggeringCard is null &&
            cardSource is not null &&
            applier == Owner.Creature &&
            target.Side != Owner.Creature.Side &&
            !target.HasPower<ArtifactPower>() &&
            power.Type == PowerType.Debuff)
        {
            _triggeringCard = cardSource;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource) =>
        !_finished &&
        cardSource is not null &&
        ReferenceEquals(_triggeringCard, cardSource) &&
        giver == Owner.Creature &&
        target is not null &&
        target.Side != Owner.Creature.Side &&
        power.Type == PowerType.Debuff
            ? 2m
            : 1m;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!_finished && ReferenceEquals(_triggeringCard, cardPlay.Card))
        {
            _finished = true;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        Reset();
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        var sourceLamp = (UnsettlingLamp)source;
        if (sourceLamp._triggeringCard is not { } sourceCard)
        {
            _triggeringCard = null;
            return;
        }

        if (cardMap.TryGetValue(sourceCard, out CardModel? mappedCard))
        {
            _triggeringCard = mappedCard;
            return;
        }

        if (sourceLamp._finished)
        {
            _triggeringCard = null;
            return;
        }

        throw new InvalidOperationException(
            "UnsettlingLamp cannot clone an active triggering card that is absent from the combat card graph.");
    }

    private void Reset()
    {
        _triggeringCard = null;
        _finished = false;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_finished);
        context.AppendCardReferences(ref builder, new CardModel?[] { _triggeringCard });
    }
}
