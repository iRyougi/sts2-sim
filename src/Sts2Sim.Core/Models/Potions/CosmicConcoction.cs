using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Potions;

public sealed class CosmicConcoction : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Cosmic Concoction requires a player target.");
        var candidates = ColorlessCardPool.Instance.GetUnlockedCards(
                player.UnlockState, player.RunState.Players.Count > 1)
            .ToList();

        foreach (CardModel generated in CardFactory.GetDistinctForCombat(
            player, candidates, 3, target.CombatState!.RunState.Rng.CombatCardGeneration))
        {
            CardCmd.Upgrade(generated);
            await CardPileCmd.Generate(target.CombatState, generated, PileType.Hand, Owner);
        }
    }
}
