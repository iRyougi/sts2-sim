using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FishingRod : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public int CombatsSeen { get; private set; }

    public override Task AfterCombatEnd()
    {
        if (Owner.RunState.CurrentRoom is not CombatRoom { RoomType: RoomType.Monster })
        {
            return Task.CompletedTask;
        }

        CombatsSeen++;
        if (CombatsSeen % 3 != 0)
        {
            return Task.CompletedTask;
        }

        CardModel[] upgradable = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToArray();
        if (upgradable.Length > 0)
        {
            CardCmd.Upgrade(Owner.RunState.Rng.Niche.NextItem(upgradable)!);
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(CombatsSeen);
    }
}