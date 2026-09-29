using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>Stable character-card membership with run-entry unlock and multiplayer filtering.</summary>
public abstract class CardPoolModel
{
    public virtual bool IsColorless => false;
    public abstract IReadOnlyList<CardModel> AllCards { get; }

    public IEnumerable<CardModel> GetUnlockedCards(
        PlayerUnlockState unlockState,
        bool isMultiplayer)
    {
        ArgumentNullException.ThrowIfNull(unlockState);
        IEnumerable<CardModel> cards = FilterThroughEpochs(unlockState, AllCards);
        return isMultiplayer ? cards : cards.Where(card => !card.IsMultiplayerOnly);
    }

    protected virtual IEnumerable<CardModel> FilterThroughEpochs(
        PlayerUnlockState unlockState,
        IEnumerable<CardModel> cards) => cards;
}

internal sealed class EmptyCardPool : CardPoolModel
{
    public static EmptyCardPool Instance { get; } = new();

    public override IReadOnlyList<CardModel> AllCards => Array.Empty<CardModel>();
}
