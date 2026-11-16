namespace Sts2Sim.Core.Models.Cards;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;

public sealed class Disintegration : CardModel, KnowledgeDemon.IChoosable, ICardComparisonBaseValueSource
{
    /// <summary>v0.113.1 CompareTo 视图：原版 <c>DisintegrationPower</c> 的当前 BaseValue。</summary>
    public decimal GetComparisonBaseValue(string key) => key == "DisintegrationPower"
        ? PowerAmount
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not a runtime-mutable comparison key.");

    /// <summary>原版 <c>CanonicalVars</c> 为 <c>PowerVar&lt;DisintegrationPower&gt;(6m)</c>；KnowledgeDemon 创建时改写。</summary>
    public int PowerAmount { get; set; } = 6;
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => -1;

    public Task OnChosen() => PowerCmd.Apply<DisintegrationPower>(
        CombatState!, Owner.Creature, PowerAmount, null, this);

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(PowerAmount);
}
