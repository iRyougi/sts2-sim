using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class StampedePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterAutoPostPlayPhaseEntered(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player)) return;
        for (int i = 0; i < Amount; i++)
        {
            CardModel? selected = Owner.Player!.RunState.Rng.Shuffle.NextItem(
                Owner.Player.PlayerCombatState!.Hand.Cards
                    .Where(card => card.Type == CardType.Attack &&
                                   !card.Keywords.Contains(CardKeyword.Unplayable))
                    .ToList());
            // Native CardCmd.AutoPlay checks the ending/death gate only after the
            // Shuffle-stream selection has already happened in this loop.
            if (selected is not null &&
                !Owner.CombatState!.IsOverOrEnding() && !Owner.IsDead)
                await AutoPlayCmd.FromCards(Owner.CombatState!, player, [selected]);
        }
    }
}
