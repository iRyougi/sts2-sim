using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>Modeled official colorless pool filtered by the player's run-entry unlock snapshot.</summary>
public sealed class ColorlessCardPool : CardPoolModel
{
    public static ColorlessCardPool Instance { get; } = new();
    public override bool IsColorless => true;
    public override IReadOnlyList<CardModel> AllCards => ModelDb.All<CardModel>().Where(c => c.IsColorless).ToArray();
    protected override IEnumerable<CardModel> FilterThroughEpochs(PlayerUnlockState unlockState,
        IEnumerable<CardModel> cards) => cards.Where(unlockState.IsColorlessCardUnlocked);

    public static IEnumerable<CardModel> GetUnlockedCards(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        IEnumerable<CardModel> unlocked = ModelDb.All<CardModel>()
            .Where(player.UnlockState.IsColorlessCardUnlocked);
        return player.RunState.Players.Count > 1
            ? unlocked
            : unlocked.Where(card => !card.IsMultiplayerOnly);
    }
}
