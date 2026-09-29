using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class ThisOrThat : EventModel
{
    private int _gold;
    protected override void CalculateVars() => _gold = Rng.NextInt(41, 69);
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("PLAIN", Plain), new("ORNATE", Ornate)];

    private async Task Plain()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, 6, ValueProp.Unblockable | ValueProp.Unpowered);
        await PlayerCmd.GainGold(_gold, Owner);
        Finish();
    }

    private async Task Ornate()
    {
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner), Owner);
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Clumsy>()], Owner);
        Finish();
    }
}
