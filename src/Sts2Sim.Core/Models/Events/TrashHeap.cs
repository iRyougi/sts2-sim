using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class TrashHeap : EventModel
{
    private static readonly Type[] RelicPool =
    [
        typeof(DarkstonePeriapt),
        typeof(DreamCatcher),
        typeof(HandDrill),
        typeof(MawBank),
        typeof(TheBoot),
    ];

    private static readonly Type[] CardPool =
    [
        typeof(Caltrops),
        typeof(Clash),
        typeof(Distraction),
        typeof(DualWield),
        typeof(Entrench),
        typeof(HelloWorld),
        typeof(Outmaneuver),
        typeof(Rebound),
        typeof(RipAndTear),
        typeof(Stack),
    ];

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Creature.CurrentHp > 5);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("DIVE_IN", DiveInAsync),
        new EventOption("GRAB", GrabAsync),
    ];

    private async Task DiveInAsync()
    {
        await CreatureCmd.Damage(
            RunState,
            Owner.Creature,
            8m,
            ValueProp.Unblockable | ValueProp.Unpowered);

        Type relicType = Rng.NextItem(RelicPool)
            ?? throw new InvalidOperationException("Trash Heap relic pool was empty.");
        await RelicCmd.Obtain((RelicModel)ModelDb.Get(relicType), Owner);
        Finish();
    }

    private async Task GrabAsync()
    {
        await PlayerCmd.GainGold(100m, Owner);
        Type cardType = Rng.NextItem(CardPool)
            ?? throw new InvalidOperationException("Trash Heap card pool was empty.");
        CardModel card = (CardModel)ModelDb.Get(cardType).MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
        Finish();
    }
}
