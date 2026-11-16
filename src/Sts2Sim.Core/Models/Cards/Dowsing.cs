using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Deviation #156: completing Dowsing performs the gameplay transform into Abundance, but
/// omits quest-completion history and visual bookkeeping because the simulator has no equivalent system.
/// </summary>
public sealed class Dowsing : CardModel, ICardComparisonBaseValueSource
{
    /// <summary>v0.113.1 CompareTo 视图：原版 <c>Rooms</c> 的当前 BaseValue。</summary>
    public decimal GetComparisonBaseValue(string key) => key == "Rooms"
        ? RoomsRemaining
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not a runtime-mutable comparison key.");

    public const int MaxRooms = 4;
    private int _unknownRoomsEntered;

    public int RoomsEntered => _unknownRoomsEntered;
    public int RoomsRemaining => Math.Max(1, MaxRooms - RoomsEntered);

    public override CardType Type => CardType.Quest;
    public override CardRarity Rarity => CardRarity.Quest;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    public override async Task BeforeRoomEntered(AbstractRoom room)
    {
        if (Pile?.Type != PileType.Deck ||
            Owner.RunState is not Runs.RunState runState ||
            runState.CurrentRoomCount > 1 ||
            runState.CurrentMapPoint?.PointType != Map.MapPointType.Unknown)
        {
            return;
        }

        _unknownRoomsEntered++;
        if (_unknownRoomsEntered >= MaxRooms)
        {
            await Commands.CardCmd.CreateAndTransform<Abundance>(this);
        }
    }

    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes)
    {
        var result = new HashSet<RoomType>(roomTypes);
        if (RoomsEntered >= MaxRooms - 1)
        {
            result.Remove(RoomType.Monster);
            result.Remove(RoomType.Elite);
        }
        return result;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_unknownRoomsEntered);
    }
}
