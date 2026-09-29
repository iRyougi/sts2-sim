namespace Sts2Sim.Core.Rl;

/// <summary>Reward components emitted by one tracker drain.</summary>
public sealed record RewardComponents(
    double Floor,
    double Act,
    double Hp,
    double Terminal)
{
    public double Total => Floor + Act + Hp + Terminal;
}
