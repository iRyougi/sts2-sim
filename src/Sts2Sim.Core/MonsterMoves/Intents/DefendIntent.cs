namespace Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>Defend intent data; rendering-only sprite and label fields are omitted.</summary>
public sealed class DefendIntent : AbstractIntent
{
    public override IntentType IntentType => IntentType.Defend;
}
