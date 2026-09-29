using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class GamblersBrew : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var player = target.Player!;
        var hand = player.PlayerCombatState!.Hand;
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            target.CombatState!, player, hand.Cards, 0, hand.Cards.Count, this);
        await CardCmd.DiscardAndDraw(selected, selected.Count);
    }
}
