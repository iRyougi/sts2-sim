using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>After combat, randomly upgrades a number of distinct upgradable cards in the owner's deck.</summary>
public sealed class ImprovementPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCombatEnd()
    {
        List<CardModel> candidates = Owner.Player!.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        for (int i = 0; i < Amount && candidates.Count > 0; i++)
        {
            CardModel card = Owner.Player.RunState.Rng.CombatCardSelection.NextItem(candidates)!;
            candidates.Remove(card);
            CardCmd.Upgrade(card);
        }
        return Task.CompletedTask;
    }
}
