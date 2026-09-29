using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Potions;

public sealed class FoulPotion : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Event;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => Owner?.Creature.CombatState is null
        ? TargetType.TargetedNoCreature : TargetType.AllEnemies;
    public override bool PassesCustomUsabilityCheck => Owner.Creature.CombatState is not null ||
        Owner.RunState.CurrentRoom switch
        {
            MerchantRoom merchant => merchant.IsMerchantTargetAvailable(Owner),
            EventRoom { Event: FakeMerchant fake } => fake.IsMerchantTargetAvailable,
            _ => false,
        };

    protected override async Task OnUse(Creature? target)
    {
        if (Owner.Creature.CombatState is { } combat)
            await CreatureCmd.Damage(combat, combat.Creatures.Where(IsAffectedCreature), 12m,
                ValueProp.Unpowered, Owner.Creature, null, null);
        else if (Owner.RunState.CurrentRoom is MerchantRoom)
            await PlayerCmd.GainGold(100, Owner);
        else if (Owner.RunState.CurrentRoom is EventRoom { Event: FakeMerchant fake })
            await fake.FoulPotionThrown(this);
    }

    private static bool IsAffectedCreature(Creature creature) => !creature.IsPet;
}
