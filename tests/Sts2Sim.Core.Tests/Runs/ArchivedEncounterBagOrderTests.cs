using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class ArchivedEncounterBagOrderTests : IDisposable
{
    public ArchivedEncounterBagOrderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public void ArchivedRuns_FirstRegularAndGloryEliteEncountersMatchHistory()
    {
        // Independent run_history observations: the third ordinary encounter follows two weak slots.
        // Expected names are archive identities, never derived from the implementation's encounter pools.
        (string Seed, int ActIndex, string Expected)[] cases =
        [
            ("R8P62QK7DEDJ", 1, "HunterKillerNormal"), // F22
            ("7C49CFEFYKJY", 1, "ChompersNormal"), // F21
            ("X2NV9ZTEG0Y6", 1, "ChompersNormal"), // F22
            ("XWDT4877APQF", 2, "OwlMagistrateNormal"), // F42
        ];
        var actual = new List<string>();
        foreach (var observation in cases)
        {
            RunState run = CreateRun(observation.Seed, observation.ActIndex);
            run.PullNextEncounter(RoomType.Monster);
            run.PullNextEncounter(RoomType.Monster);
            actual.Add(run.PullNextEncounter(RoomType.Monster).Name);
        }

        // X2's two archived Glory elite rooms are Knights followed by SoulNexus.
        RunState eliteRun = CreateRun("X2NV9ZTEG0Y6", 2);
        actual.Add(eliteRun.PullNextEncounter(RoomType.Elite).Name);
        actual.Add(eliteRun.PullNextEncounter(RoomType.Elite).Name);
        Assert.Equal(cases.Select(observation => observation.Expected)
            .Concat(new[] { "KnightsElite", "SoulNexusElite" }), actual);
    }

    private static RunState CreateRun(string seed, int actIndex)
    {
        var run = new RunState(seed, ActDefinition.GetRandomList(seed), ascensionLevel: 10);
        run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), run));
        for (int i = 0; i < actIndex; i++)
            run.AdvanceToNextAct();
        return run;
    }

    public void Dispose() => ModelDb.ResetForTests();
}
