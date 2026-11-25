namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

/// <summary>v0.114.0 删除了 <c>ShouldPowerBeRemovedAfterOwnerDeath</c> 与 <c>ShouldCreatureBeRemovedFromCombatAfterDeath</c>
/// 两个 override：持有者在攻击中死亡时，能力由攻击/行动收尾统一清理，AfterAttack 仍能看到本次攻击。</summary>
public sealed class PainfulStabsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAttack(AttackCommand command)
    {
        if (command.Attacker != Owner || command.TargetSide == Owner.Side || !command.DamageProps.IsPoweredAttack())
        {
            return;
        }

        foreach (IGrouping<Creature, DamageResult> group in command.Results
                     .SelectMany(results => results)
                     .Where(result => result.Receiver.Player is not null && result.UnblockedDamage > 0)
                     .GroupBy(result => result.Receiver))
        {
            int woundCount = checked(Amount * group.Count());
            for (int index = 0; index < woundCount; index++)
            {
                var wound = (Wound)ModelDb.Card<Wound>().MutableClone();
                wound.AssignOwner(group.Key.Player!);
                await CardPileCmd.Generate(Owner.CombatState!, wound, PileType.Discard, creator: null);
            }
        }
    }
}
