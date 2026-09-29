using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>BorrowedTime</c>：1 费技能，获得 4（升级 6）能量，再施加 1 层 <see cref="BorrowedTimePower"/>
/// （本回合自己的牌费用 +1）。</summary>
public sealed class BorrowedTime : CardModel, ICardChoiceBaseValueProvider
{
    private const decimal ExtraCost = 1m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal Energy => IsUpgraded ? 6m : 4m;

    // ExtraCost 是另一个 EnergyVar 的自定义键，不是字面 Energy。
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: (double)Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(Energy, Owner);
        await PowerCmd.Apply<BorrowedTimePower>(CombatState!, Owner.Creature, ExtraCost, Owner.Creature, this);
    }
}
