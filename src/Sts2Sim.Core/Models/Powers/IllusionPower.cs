using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Keeps an illusion in combat after death so it can spend one move reviving.</summary>
public sealed class IllusionPower : PowerModel
{
    private string? _followUpStateId;
    private bool _isReviving;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public string? FollowUpStateId
    {
        get => _followUpStateId;
        set
        {
            AssertMutable();
            _followUpStateId = value;
        }
    }

    public bool IsReviving => _isReviving;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        Owner.HasPower<MinionPower>()
            ? Task.CompletedTask
            : PowerCmd.Apply<MinionPower>(Owner.CombatState!, Owner, 1m, null, null);

    public override bool ShouldPowerBeRemovedOnDeath(PowerModel power)
    {
        if (power.Type == PowerType.Debuff)
        {
            return power is not ITemporaryPower;
        }

        return false;
    }

    public override Task AfterDeath(Creature target)
    {
        if (target != Owner)
        {
            return Task.CompletedTask;
        }

        _isReviving = true;
        MonsterModel monster = Owner.Monster!;
        string followUpStateId = FollowUpStateId ?? monster.MoveStateMachine!.StateLog.Last().Id;
        monster.SetMoveImmediate(CreateReviveMove(followUpStateId));
        return Task.CompletedTask;
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Owner || !IsReviving;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        creature != Owner;

    internal MoveState CreateReviveMove(string followUpStateId)
    {
        return new MoveState("REVIVE_MOVE", ReviveMove, new HealIntent())
        {
            FollowUpStateId = followUpStateId,
            MustPerformOnceBeforeTransitioning = true,
            CombatCloneFactory = clonedMonster =>
            {
                IllusionPower clonedPower = clonedMonster.Creature.GetPower<IllusionPower>()
                    ?? throw new InvalidOperationException(
                        "A reviving monster clone must own IllusionPower.");
                return clonedPower.CreateReviveMove(followUpStateId);
            },
        };
    }

    private async Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        _isReviving = false;
        await CreatureCmd.Heal(Owner, Owner.MaxHp - Owner.CurrentHp);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_followUpStateId);
        builder.Append(_isReviving);
    }
}
