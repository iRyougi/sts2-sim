namespace Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>Waterfall Giant and Gas Bomb's single attack with a death-blow intent marker.</summary>
public sealed class DeathBlowIntent : SingleAttackIntent
{
    public DeathBlowIntent(Func<decimal> damageCalc)
        : base(damageCalc)
    {
    }

    public override IntentType IntentType => IntentType.DeathBlow;
}
