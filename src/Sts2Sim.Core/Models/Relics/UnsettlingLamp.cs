using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class UnsettlingLamp : RelicModel
{
    private CardModel? _triggeringCard;
    private Type? _doubledTemporaryInternalPowerType;
    private bool _finished;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeCombatStart()
    {
        Reset();
        Status = RelicStatus.Active;
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        CardModel? cardSource)
    {
        // 偏离 #130：the simulator still has no power visibility surface (power.IsVisible), so that
        // upstream exclusion remains unrepresented (it only hides ExtraTurnPower / ImpaledPower).
        // 偏离 #309（2026-09-08）：ArtifactPower 现已存在，恢复权威的 target.HasPower<ArtifactPower>() 排除
        // ——被 Artifact 挡掉的 debuff 不该让本遗物进入触发态。
        // #1041：原版按 power.GetTypeForAmount(amount) 判断减益（负力量算减益），不是 power.Type。
        if (!_finished &&
            _triggeringCard is null &&
            cardSource is not null &&
            applier == Owner.Creature &&
            target.Side != Owner.Creature.Side &&
            power.GetTypeForAmount(amount) == PowerType.Debuff &&
            !target.HasPower<ArtifactPower>())
        {
            _triggeringCard = cardSource;
            // 原版 DoubledPowers.Add(power)：只在进入触发态时加入，且开战/战斗结束时清空，所以列表至多一个元素；
            // 它只被 HasDoubledTemporaryPowerSource 读取（ITemporaryPower 的内部能力类型），这里只存那个类型。
            _doubledTemporaryInternalPowerType = (power as ITemporaryPower)?.InternallyAppliedPower.GetType();
        }

        return Task.CompletedTask;
    }

    // 原版 ModifyPowerAmountGivenMultiplicative 只比对 cardSource == TriggeringCard，不检查施加者与目标阵营。
    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource) =>
        _triggeringCard is not null &&
        ReferenceEquals(_triggeringCard, cardSource) &&
        !_finished &&
        !HasDoubledTemporaryPowerSource(power) &&
        power.GetTypeForAmount(amount) == PowerType.Debuff
            ? 2m
            : 1m;

    /// <summary>原版 <c>HasDoubledTemporaryPowerSource</c>：已翻倍某个 <see cref="ITemporaryPower"/>（例如
    /// PiercingWailPower）后，不再翻倍它内部施加的能力（负力量），否则会变成四倍。</summary>
    private bool HasDoubledTemporaryPowerSource(PowerModel power) =>
        _doubledTemporaryInternalPowerType is not null &&
        _doubledTemporaryInternalPowerType == power.GetType();

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!_finished && ReferenceEquals(_triggeringCard, cardPlay.Card))
        {
            _finished = true;
            Status = RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        Reset();
        Status = RelicStatus.Normal;
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
        _doubledTemporaryInternalPowerType = null;
        _finished = false;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_finished);
        builder.Append(_doubledTemporaryInternalPowerType?.FullName);
        context.AppendCardReferences(ref builder, new CardModel?[] { _triggeringCard });
    }
}
