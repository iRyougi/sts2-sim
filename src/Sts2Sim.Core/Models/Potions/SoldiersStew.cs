using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>Add one replay to every Strike-tagged card in the target's combat piles.</summary>
public sealed class SoldiersStew : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Player?.PlayerCombatState is not { } state)
            throw new InvalidOperationException("SoldiersStew requires a player in combat.");
        foreach (CardModel card in state.AllPiles.SelectMany(pile => pile.Cards)
            .Where(card => card.Tags.Contains(CardTag.Strike)).ToArray())
            card.BaseReplayCount++;
        return Task.CompletedTask;
    }
}
