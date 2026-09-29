using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class AttackPotion : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player recipient = target.Player
            ?? throw new InvalidOperationException("Attack Potion requires a player target.");
        ICombatState combatState = Owner.Creature.CombatState!;

        // 偏离 #68/#131 已销案（偏离 #319，2026-09-09）：现按权威走 Character.CardPool。
        await CardChoicePotionEffect.GenerateOneOfThree(
            combatState,
            recipient,
            Owner,
            recipient.Character.CardPool,
            card => card.Type == CardType.Attack, this);
    }
}
