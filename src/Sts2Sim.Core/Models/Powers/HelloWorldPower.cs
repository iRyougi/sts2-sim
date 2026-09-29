using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Powers;

public sealed class HelloWorldPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player || AmountOnTurnStart < 1) return;
        IEnumerable<CardModel> candidates = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1)
            .Where(card => card.Rarity == CardRarity.Common);
        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            player, candidates, AmountOnTurnStart, player.RunState.Rng.CombatCardGeneration);
        foreach (CardModel card in generated)
            await CardPileCmd.Generate(Owner.CombatState!, card, PileType.Hand, player);
    }
}
