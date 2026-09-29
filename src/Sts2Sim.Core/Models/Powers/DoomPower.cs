using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>DoomPower</c>：持有者当前生命不超过层数时，在它所在一方的回合结束时被击杀
/// （敌方在 BeforeSideTurnEnd，玩家方在 AfterSideTurnEnd）。同一方所有被判定的生物由排在最前的那个持有者
/// 一次性结算，随后派发 <see cref="Hook.AfterDiedToDoom"/>。击杀走普通的 <see cref="CreatureCmd.Kill"/>，
/// 多阶段怪物由 ShouldDie 类钩子挡住；原版的 ShouldDisappearFromDoom / OnDieToDoom 只影响动画。</summary>
public sealed class DoomPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public static async Task DoomKill(IReadOnlyList<Creature> creatures)
    {
        if (creatures.Count == 0)
            return;

        ICombatState combatState = creatures[0].CombatState
            ?? throw new InvalidOperationException("Doomed creature has no combat state.");
        foreach (Creature creature in creatures)
        {
            await CreatureCmd.Kill(creature);
        }

        await Hook.AfterDiedToDoom(combatState, creatures);
    }

    public static IReadOnlyList<Creature> GetDoomedCreatures(IReadOnlyList<Creature> creatures) =>
        creatures.Where(creature => creature.GetPower<DoomPower>()?.IsOwnerDoomed() ?? false).ToList();

    public override async Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player && ShouldDoomTrigger(participants))
            await DoomKill(GetDoomedCreatures(side));
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy && ShouldDoomTrigger(participants))
            await DoomKill(GetDoomedCreatures(side));
    }

    private bool ShouldDoomTrigger(IEnumerable<Creature> participants)
    {
        if (Owner.CombatState is not { } combatState || combatState.IsOverOrEnding())
            return false;
        if (!participants.Contains(Owner) || Owner.IsDead || !IsOwnerDoomed())
            return false;
        return GetDoomedCreatures(Owner.Side).First() == Owner;
    }

    private IReadOnlyList<Creature> GetDoomedCreatures(CombatSide side) =>
        GetDoomedCreatures(Owner.CombatState!.GetCreaturesOnSide(side));

    private bool IsOwnerDoomed() => Owner.CurrentHp <= Amount;
}
