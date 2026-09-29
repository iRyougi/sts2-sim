namespace Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>Debuff intent data; rendering-only sprite and label fields are omitted.</summary>
public sealed class DebuffIntent(bool strong = false) : AbstractIntent
{
    public override IntentType IntentType => strong ? IntentType.DebuffStrong : IntentType.Debuff;
}
