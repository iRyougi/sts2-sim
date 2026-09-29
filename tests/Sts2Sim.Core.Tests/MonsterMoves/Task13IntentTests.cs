namespace Sts2Sim.Core.Tests.MonsterMoves;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Task13IntentTests
{
    [Fact]
    public void DefendIntent_ReportsDefendType()
    {
        var intent = new DefendIntent();

        Assert.Equal(IntentType.Defend, intent.IntentType);
    }

    [Fact]
    public void DebuffIntent_ReportsNormalAndStrongTypes()
    {
        var normal = new DebuffIntent();
        var strong = new DebuffIntent(strong: true);

        Assert.Equal(IntentType.Debuff, normal.IntentType);
        Assert.Equal(IntentType.DebuffStrong, strong.IntentType);
    }

    [Fact]
    public void MultiAttackIntent_ReportsDamageRepeatsAndTotalDamage()
    {
        var intent = new MultiAttackIntent(damage: 3, repeat: 8);
        Creature owner = Creature.CreateStandaloneForTests(10, 10);

        Assert.Equal(IntentType.Attack, intent.IntentType);
        Assert.Equal(3, intent.GetSingleDamage(Array.Empty<Creature>(), owner));
        Assert.Equal(8, intent.Repeats);
        Assert.Equal(24, intent.GetTotalDamage(Array.Empty<Creature>(), owner));
    }
}
