using Sts2Sim.Core.Combat;
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
    // Native checks CombatManager.Instance.IsInProgress, not the creature's combat-state reference: a combat-layout event
    // (THE_LANTERN_KEY) prepares its combat at entry and registers the player there before any fight starts.
    private bool CombatInProgress => Owner?.Creature.CombatState is CombatState { Engine: { IsInProgress: true } };

    public override TargetType TargetType => !CombatInProgress
        ? TargetType.TargetedNoCreature : TargetType.AllEnemies;
    public override bool PassesCustomUsabilityCheck => CombatInProgress ||
        Owner.RunState.CurrentRoom switch
        {
            MerchantRoom merchant => merchant.IsMerchantTargetAvailable(Owner),
            EventRoom { Event: FakeMerchant fake } => fake.IsMerchantTargetAvailable,
            _ => false,
        };

    protected override async Task OnUse(Creature? target)
    {
        if (CombatInProgress && Owner.Creature.CombatState is { } combat)
            await CreatureCmd.Damage(combat, combat.Creatures.Where(IsAffectedCreature), 12m,
                ValueProp.Unpowered, Owner.Creature, null, null);
        else if (Owner.RunState.CurrentRoom is MerchantRoom)
            await PlayerCmd.GainGold(100, Owner);
        else if (Owner.RunState.CurrentRoom is EventRoom { Event: FakeMerchant fake })
            await fake.FoulPotionThrown(this);
    }

    private static bool IsAffectedCreature(Creature creature) => !creature.IsPet;
}
