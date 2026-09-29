using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SpiritOfAsh</c>：1 费能力，获得 4（升级 5）层 <see cref="SpiritOfAshPower"/>。</summary>
public sealed class SpiritOfAsh : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal BlockOnExhaust => IsUpgraded ? 5m : 4m;

    // 原版键名 BlockOnExhaust 不是字面 Block。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<SpiritOfAshPower>(CombatState!, Owner.Creature, BlockOnExhaust, Owner.Creature, this);
}
