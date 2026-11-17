using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>进入回合末自动出牌前，按叠加层数多次从手牌最低费可用牌中随机自动打出（原版 <c>SimpletonPower</c>）。</summary>
public sealed class SimpletonPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAutoPostPlayPhaseEntered(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
            return;

        CardPile hand = player.PlayerCombatState!.Hand;
        for (int i = 0; i < Amount; i++)
        {
            IGrouping<int, CardModel>? lowestCostGroup = hand.Cards
                .Where(c => !c.Keywords.Contains(CardKeyword.Unplayable))
                .GroupBy(c => c.CostsXEnergy ? 999_999_999 : c.ResolvedEnergyCostForComparison)
                .OrderBy(g => g.Key)
                .FirstOrDefault();
            if (lowestCostGroup is null)
                continue;

            CardModel? picked = player.RunState.Rng.CombatCardSelection.NextItem(lowestCostGroup);
            if (picked is not null && !Owner.CombatState!.IsOverOrEnding() && !Owner.IsDead)
            {
                await AutoPlayCmd.FromCards(Owner.CombatState!, player, [picked]);
            }
        }
    }
}
