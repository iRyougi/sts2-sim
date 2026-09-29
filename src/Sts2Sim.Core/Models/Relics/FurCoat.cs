using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FurCoat : RelicModel
{
    private int _actIndex = -1;
    private HashSet<MapCoord> _markedCoordinates = [];

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public IReadOnlyList<MapCoord> MarkedCoordinates => _markedCoordinates
        .OrderBy(coord => coord.row)
        .ThenBy(coord => coord.col)
        .ToArray();

    public override Task AfterObtained()
    {
        if (Owner.RunState is not RunState runState)
        {
            throw new InvalidOperationException("Fur Coat requires the standard run state to mark the current act map.");
        }

        _actIndex = runState.CurrentActIndex;
        int ownerIndex = Owner.RunState.Players.ToList().IndexOf(Owner);
        var rng = new Rng(
            Owner.RunState.Rng.Seed + (ulong)ownerIndex + StringHelper.GetDeterministicHashCode(Id.Entry));
        if (RngDiagnostics.DrawObserver is not null)
            rng.WithDiagnosticContentId(Id.ToString());
        List<MapPoint> candidates = runState.Map.GetAllMapPoints()
            .Where(point => point.PointType is MapPointType.Monster or MapPointType.Elite)
            .ToList();
        candidates.UnstableShuffle(rng);
        _markedCoordinates = candidates.Take(8).Select(point => point.coord).ToHashSet();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        if (!IsMarkedCurrentMapPoint())
        {
            return;
        }

        foreach (var enemy in Owner.Creature.CombatState!.HittableEnemies)
        {
            await CreatureCmd.SetCurrentHp(enemy, 1m);
        }
    }

    public override Task AfterCreatureAddedToCombat(Creature creature) =>
        IsMarkedCurrentMapPoint() && creature.Side == CombatSide.Enemy
            ? CreatureCmd.SetCurrentHp(creature, 1m)
            : Task.CompletedTask;

    private bool IsMarkedCurrentMapPoint() =>
        Owner.RunState is RunState runState &&
        _actIndex == runState.CurrentActIndex &&
        runState.CurrentMapPoint is { } point &&
        _markedCoordinates.Contains(point.coord);

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _markedCoordinates = new HashSet<MapCoord>(_markedCoordinates);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_actIndex);
        builder.Append(_markedCoordinates.Count);
        foreach (MapCoord coordinate in MarkedCoordinates)
        {
            builder.Append(coordinate.col);
            builder.Append(coordinate.row);
        }
    }
}
