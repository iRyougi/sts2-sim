using Sts2Sim.Core.Content;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Models.Events;

internal sealed class HiveTestAct2 : ActDefinition
{
    private static readonly EncounterDefinition Encounter = new(
        () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
    public override int Index => 1;
    public override IReadOnlyList<Type> EventPool => [typeof(Amalgamator)];
    public override IReadOnlyList<Type> AncientPool => [typeof(Orobas)];
    public override int BaseNumberOfRooms => 15;
    public override int NumberOfWeakEncounters => 0;
    protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => [Encounter];
    protected override IReadOnlyList<EncounterDefinition> EliteEncounters => [Encounter];
    protected override IReadOnlyList<EncounterDefinition> BossEncounters => [Encounter];
    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
}
