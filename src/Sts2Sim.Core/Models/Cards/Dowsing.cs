using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Deviation #156: completing Dowsing performs the gameplay transform into Abundance, but
/// omits quest-completion history and visual bookkeeping because the simulator has no equivalent system.
/// </summary>
public sealed class Dowsing : CardModel
{
    private int _unknownRoomsEntered;

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
        if (_unknownRoomsEntered >= 5)
        {
            await Commands.CardCmd.CreateAndTransform<Abundance>(this);
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_unknownRoomsEntered);
    }
}
