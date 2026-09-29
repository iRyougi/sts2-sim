using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Commands;

/// <summary>
/// 储君熔炼共享机制。偏离 #89：省略 VFX/SFX、多人 LocalContext 判定、预览动画与 AfterForge hook；
/// 保留生成和强化 SovereignBlade 的结算行为。
/// </summary>
public static class ForgeCmd
{
    public static async Task Forge(decimal amount, Player player, AbstractModel? source)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (GetSovereignBlades(player, includeExhausted: false).Count == 0)
        {
            var blade = (SovereignBlade)ModelDb.Card<SovereignBlade>().MutableClone();
            blade.AssignOwner(player);
            await CardPileCmd.Generate(
                player.Creature.CombatState
                    ?? throw new InvalidOperationException("Forge requires an active combat."),
                blade,
                PileType.Hand,
                player);
        }

        foreach (SovereignBlade blade in GetSovereignBlades(player, includeExhausted: true))
        {
            blade.AddDamage(amount);
        }

        await Hook.AfterForge(player.Creature.CombatState!, amount, player, source);
    }

    private static List<SovereignBlade> GetSovereignBlades(Player player, bool includeExhausted)
    {
        IEnumerable<CardModel> cards = player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards);
        if (!includeExhausted)
        {
            cards = cards.Where(card => card.Pile?.Type != PileType.Exhaust);
        }

        return cards.OfType<SovereignBlade>().ToList();
    }
}
