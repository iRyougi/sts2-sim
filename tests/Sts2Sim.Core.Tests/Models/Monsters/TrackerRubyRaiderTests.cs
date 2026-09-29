namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class TrackerRubyRaiderTests : IDisposable
{
    private readonly Task13MonsterTestFixture _fixture = new(typeof(TrackerRubyRaider));

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 21, 25)]
    [InlineData((int)AscensionLevel.ToughEnemies, 22, 26)]
    public void HpRange_UsesToughEnemiesValues(int ascension, int expectedMin, int expectedMax)
    {
        (TrackerRubyRaider monster, _) = Task13MonsterTestFixture.CreateCombat<TrackerRubyRaider>(ascension);

        Assert.Equal(expectedMin, monster.MinInitialHp);
        Assert.Equal(expectedMax, monster.MaxInitialHp);
        Assert.InRange(monster.Creature.MaxHp, expectedMin, expectedMax);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9)]
    public async Task TrackHitsAllTargetsOnceThenHoundsSelfLoops(int ascension, int expectedRepeats)
    {
        (TrackerRubyRaider monster, var targets) =
            Task13MonsterTestFixture.CreateCombat<TrackerRubyRaider>(ascension, targetCount: 2);

        Assert.Equal("TRACK_MOVE", monster.NextMove!.StateId);
        Assert.Equal(IntentType.Debuff, Assert.IsType<DebuffIntent>(Assert.Single(monster.NextMove.Intents)).IntentType);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target => Assert.Equal(2, target.GetPower<FrailPower>()!.Amount));
        Assert.Equal("HOUNDS_MOVE", monster.NextMove!.StateId);
        var houndsIntent = Assert.IsType<MultiAttackIntent>(Assert.Single(monster.NextMove.Intents));
        Assert.Equal(1, houndsIntent.GetSingleDamage(targets, monster.Creature));
        Assert.Equal(expectedRepeats, houndsIntent.Repeats);
        Assert.Equal(expectedRepeats, houndsIntent.GetTotalDamage(targets, monster.Creature));

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target => Assert.Equal(200 - expectedRepeats, target.CurrentHp));
        Assert.Equal("HOUNDS_MOVE", monster.NextMove!.StateId);

        await Task13MonsterTestFixture.PerformAndRoll(monster, targets);
        Assert.All(targets, target => Assert.Equal(200 - (2 * expectedRepeats), target.CurrentHp));
        Assert.Equal("HOUNDS_MOVE", monster.NextMove!.StateId);
        Assert.Equal(1, monster.MoveStateMachine!.StateLog.Count(state => state.Id == "TRACK_MOVE"));
        Assert.All(targets, target => Assert.Equal(2, target.GetPower<FrailPower>()!.Amount));
    }
}
