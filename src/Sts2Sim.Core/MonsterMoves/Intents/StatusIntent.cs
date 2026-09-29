namespace Sts2Sim.Core.MonsterMoves.Intents;

public sealed class StatusIntent(int count) : AbstractIntent
{
    public int Count { get; } = count;

    public override IntentType IntentType => IntentType.StatusCard;
}
