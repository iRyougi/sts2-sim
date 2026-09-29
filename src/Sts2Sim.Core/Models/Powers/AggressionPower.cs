using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class AggressionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner)) return Task.CompletedTask;
        List<CardModel> attacks = Owner.Player!.PlayerCombatState!.DiscardPile.Cards
            .Where(card => card.Type == CardType.Attack).ToList()
            .UnstableShuffle(Owner.Player.RunState.Rng.CombatCardSelection);
        foreach (CardModel card in attacks.Take(Amount))
        {
            CardPileCmd.Add(card, PileType.Hand);
            CardCmd.Upgrade(card);
        }
        return Task.CompletedTask;
    }
}
