using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

public sealed record CloneRestSiteOption : RestSiteDecision
{
    public override string OptionId => "clone";
    public override int Priority => 4;

    public override async Task ExecuteAsync(Player player)
    {
        CardModel[] originals = player.Deck.Cards
            .Where(card => card.Enchantments.Any(enchantment => enchantment is Clone)).ToArray();
        foreach (CardModel original in originals)
        {
            var copy = (CardModel)original.MutableClone();
            await CardPileCmd.AddToDeck(copy);
        }
    }
}
