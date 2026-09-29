using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rewards;

/// <summary>Returns one specific mutable card to its owner's permanent deck.</summary>
public sealed class SpecialCardReward : TakeableReward
{
    public CardModel Card { get; }

    public SpecialCardReward(CardModel card, Player player) : base(player)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(player);
        card.AssertMutable();
        if (!ReferenceEquals(card.Owner, player))
        {
            throw new ArgumentException("The special card reward must be offered to the card owner.", nameof(player));
        }

        Card = card;
    }

    public override void Populate(IRunState runState)
    {
    }

    protected override Task OnTake() => CardPileCmd.AddToDeck(Card);
}
