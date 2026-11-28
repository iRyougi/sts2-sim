using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Dread;

public abstract class DominionModel : AbstractModel
{
    private IRunState? _runState;
    private IReadOnlyList<Player>? _playersEnteringCombat;

    public override bool ShouldReceiveCombatHooks => true;

    public IRunState? RunState
    {
        get
        {
            AssertMutable();
            return _runState;
        }
        internal set
        {
            AssertMutable();
            if (_runState is not null && !ReferenceEquals(_runState, value))
            {
                throw new InvalidOperationException("Run State has already been set.");
            }
            _runState = value;
        }
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom { CombatState: { } combatState })
        {
            return Task.CompletedTask;
        }

        AssertMutable();
        _playersEnteringCombat = combatState.Players;
        return AfterEnemiesEnteredCombat(combatState.Enemies);
    }

    public override Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.Side == CombatSide.Player)
        {
            return Task.CompletedTask;
        }

        return AfterEnemiesEnteredCombat([creature]);
    }

    public override async Task BeforeCombatStart()
    {
        if (_playersEnteringCombat is { } players)
        {
            AssertMutable();
            _playersEnteringCombat = null;
            await AfterPlayersEnteredCombat(players);
        }
    }

    protected virtual Task AfterEnemiesEnteredCombat(IEnumerable<Creature> creatures) =>
        Task.CompletedTask;

    protected virtual Task AfterPlayersEnteredCombat(IReadOnlyList<Player> players) =>
        Task.CompletedTask;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _runState = null;
        _playersEnteringCombat = null;
    }

    internal DominionModel CloneForGraph(IRunState targetRun, IReadOnlyDictionary<Player, Player> playerMap)
    {
        var clone = (DominionModel)MutableClone();
        clone.RunState = targetRun;
        if (_playersEnteringCombat is { } pending)
        {
            clone._playersEnteringCombat = Array.AsReadOnly(
                pending.Select(player => playerMap[player]).ToArray());
        }
        return clone;
    }
}
