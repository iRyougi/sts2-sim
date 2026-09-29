using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class CreativeAiPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player)) return;
        for (int i = 0; i < Amount; i++)
        {
            CardModel? generated = CardFactory.GetDistinctForCombat(
                player,
                player.Character.CardPool.GetUnlockedCards(
                    player.UnlockState, player.RunState.Players.Count > 1)
                    .Where(card => card.Type == CardType.Power),
                1,
                player.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (generated is not null)
                await CardPileCmd.Generate(Owner.CombatState!, generated, PileType.Hand, player);
        }
    }
}
