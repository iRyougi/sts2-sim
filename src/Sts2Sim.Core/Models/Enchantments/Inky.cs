using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Enchantments;

/// <summary>Inky applies one Weak to each resolved card target after the card effect.</summary>
public sealed class Inky : EnchantmentModel
{
    public override async Task OnPlay(CardModel card, CardPlay cardPlay)
    {
        if (!ReferenceEquals(card, Owner))
        {
            return;
        }

        IReadOnlyList<Creature> targets = card.TargetType == TargetType.AllEnemies
            ? card.CombatState!.HittableEnemies.ToArray()
            : cardPlay.Target is null
                ? Array.Empty<Creature>()
                : new[] { cardPlay.Target };
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(
                card.CombatState!,
                target,
                1m,
                card.Owner.Creature,
                card);
        }
    }
}
