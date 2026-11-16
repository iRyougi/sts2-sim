using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class GloryBossOrderReferenceTests : IDisposable
{
    public GloryBossOrderReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact(Skip = "v0.111.0 archived run; v0.113.1 act event pools (#927) change the up-front room-generation RNG; same-version reference pending #930")]
    public void ArchivedA10SilentRun_GloryBossesAreQueenThenTestSubject()
    {
        const string seed = "8F9CPYQ6QYEN";
        var run = new RunState(seed, ActDefinition.GetRandomList(seed), ascensionLevel: 10);
        run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), run));
        run.AdvanceToNextAct();
        run.AdvanceToNextAct();

        // run_history.json F48/F49: QUEEN_BOSS followed by TEST_SUBJECT_BOSS.
        // Rooms are generated through the real initialization order; no RNG injection.
        Assert.Equal(new[] { "QueenBoss", "TestSubjectBoss" },
            Enumerable.Range(0, 2).Select(_ => run.PullNextEncounter(RoomType.Boss).Name));
    }

    public void Dispose() => ModelDb.ResetForTests();
}
