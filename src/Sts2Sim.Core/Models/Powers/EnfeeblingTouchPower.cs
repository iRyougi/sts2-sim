namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>EnfeeblingTouchPower</c>：临时减力量，持有者所在一方回合结束时归还。</summary>
public sealed class EnfeeblingTouchPower : TemporaryStrengthPower
{
    protected override bool IsPositive => false;
}
