using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Forces an Act 3 unknown room to Event and replaces the next event with Repy.</summary>
public sealed class LanternKey : CardModel
{
    public override CardType Type => CardType.Quest;
    public override CardRarity Rarity => CardRarity.Quest;
    public override TargetType TargetType => TargetType.Self;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) =>
        Owner.RunState is RunState { CurrentActIndex: 2 } ? new HashSet<RoomType> { RoomType.Event } : roomTypes;

    public override EventModel ModifyNextEvent(EventModel currentEvent) =>
        Owner.RunState is RunState { CurrentActIndex: 2 } ? ModelDb.Event<WarHistorianRepy>() : currentEvent;
}
