using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

/// <summary>v0.114.0 <c>CloneRestSiteOption</c>：牌组里有带 Clone 附魔的牌时才可选（<c>IsEnabled</c> 实时求值，
/// v0.113.1 恒为可选）；由 <see cref="Clone"/> 附魔提供，同一玩家只出一个（#1110）。</summary>
public sealed record CloneRestSiteOption(Player Owner) : RestSiteDecision
{
    public override string OptionId => "clone";
    public override int Priority => 4;
    public override bool IsEnabled => Owner.Deck.Cards.Any(card => card.Enchantments.Any(enchantment => enchantment is Clone));

    public override async Task ExecuteAsync(Player player)
    {
        if (!ReferenceEquals(player, Owner))
        {
            throw new InvalidOperationException("Clone rest-site option was generated for a different player.");
        }

        CardModel[] originals = player.Deck.Cards
            .Where(card => card.Enchantments.Any(enchantment => enchantment is Clone)).ToArray();
        foreach (CardModel original in originals)
        {
            var copy = (CardModel)original.MutableClone();
            await CardPileCmd.AddToDeck(copy);
        }
    }
}
