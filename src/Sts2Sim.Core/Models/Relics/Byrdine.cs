using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 <c>MegaCrit.Sts2.Core.Models.Relics.Byrdine</c>：Byrdpip 在营火进化的结果。获得时把牌组
/// （战斗中还包括战斗牌堆）里的每张 ByrdSwoop 转化为 GodByrd，已升级的保留升级；每场战斗召唤 ByrdineMonster。
/// 偏离 #160：<c>[SavedProperty] Hair</c> 只决定皮肤，省略。</summary>
public sealed class Byrdine : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool AddsPet => true;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var swoops = Owner.Deck.Cards.OfType<ByrdSwoop>().Cast<CardModel>().ToList();
        if (Owner.Creature.CombatState is { } combatState &&
            combatState.IsLiveCombat() &&
            Owner.PlayerCombatState is { } playerCombatState)
        {
            swoops.AddRange(playerCombatState.AllPiles
                .SelectMany(pile => pile.Cards)
                .OfType<ByrdSwoop>());
        }

        foreach (CardModel swoop in swoops)
        {
            var godByrd = (GodByrd)ModelDb.Card<GodByrd>().MutableClone();
            godByrd.AssignOwner(swoop.Owner);
            if (swoop.IsUpgraded)
            {
                CardCmd.Upgrade(godByrd);
            }
            await CardCmd.Transform(swoop, godByrd);
        }

        if (Owner.Creature.CombatState?.IsLiveCombat() == true)
        {
            await SummonPet();
        }
    }

    public override Task BeforeCombatStart() => SummonPet();

    private async Task SummonPet()
    {
        await PlayerCmd.AddPet<ByrdineMonster>(Owner);
    }
}
