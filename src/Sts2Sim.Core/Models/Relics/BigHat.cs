using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>首回合开始时，从自己角色卡池里不重复地取 2 张虚无牌加入手牌（CombatCardGeneration 流）。</summary>
public sealed class BigHat : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState!.TurnNumber > 1)
            return;

        List<CardModel> ethereal = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, isMultiplayer: Owner.RunState.Players.Count > 1)
            .Where(card => card.Keywords.Contains(CardKeyword.Ethereal))
            .ToList();
        if (ethereal.Count == 0)
            return;

        ICombatState combatState = Owner.Creature.CombatState!;
        foreach (CardModel card in CardFactory.GetDistinctForCombat(
                     Owner, ethereal, 2, Owner.RunState.Rng.CombatCardGeneration))
        {
            await CardPileCmd.Generate(combatState, card, PileType.Hand, Owner);
        }
    }
}
