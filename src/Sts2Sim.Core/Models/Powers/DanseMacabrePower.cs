using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>DanseMacabrePower</c>：持有者打出一张已解析能量费用不低于 2 的牌时（打出前），获得层数的无力格挡。
/// 原版读 <c>EnergyCost.GetResolved()</c>：X 费牌取打出时锁定的 X 值（即 <see cref="CardPlay.Resources"/> 的
/// EnergyValue），其余取带全部修正的当前费用。</summary>
public sealed class DanseMacabrePower : PowerModel
{
    private const int EnergyThreshold = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
            return;

        int resolvedCost = cardPlay.Card.CostsXEnergy ? cardPlay.Resources.EnergyValue : cardPlay.Card.EnergyCost;
        if (resolvedCost >= EnergyThreshold)
            await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
    }
}
