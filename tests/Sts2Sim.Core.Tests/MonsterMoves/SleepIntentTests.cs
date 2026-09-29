namespace Sts2Sim.Core.Tests.MonsterMoves;

using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SleepIntentTests
{
    [Fact]
    public void IntentType_IsSleep()
    {
        var intent = new SleepIntent();

        Assert.Equal(IntentType.Sleep, intent.IntentType);
    }
}
