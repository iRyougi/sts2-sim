using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>在目标手牌生成 3 张 Joy，随后生成 Sadness 填满手牌上限（原版 <c>CrystallineEuphoria</c>）。</summary>
public sealed class CrystallineEuphoria : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player targetPlayer = target.Player!;
        ICombatState combatState = target.CombatState!;
        var joys = new List<CardModel>(3);
        for (int i = 0; i < 3; i++)
        {
            var joy = (Joy)ModelDb.Card<Joy>().MutableClone();
            joy.AssignOwner(targetPlayer);
            joys.Add(joy);
        }

        foreach (CardModel joy in joys)
        {
            await CardPileCmd.Generate(combatState, joy, PileType.Hand, Owner);
        }

        int sadnessCount = Math.Max(0, CardPile.MaxCardsInHand - targetPlayer.PlayerCombatState!.Hand.Cards.Count);
        var sadnessList = new List<CardModel>(sadnessCount);
        for (int i = 0; i < sadnessCount; i++)
        {
            var sadness = (Sadness)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(targetPlayer);
            sadnessList.Add(sadness);
        }

        foreach (CardModel sadness in sadnessList)
        {
            await CardPileCmd.Generate(combatState, sadness, PileType.Hand, Owner);
        }
    }
}
