namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

public sealed class PainfulStabsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        creature != Owner;

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
