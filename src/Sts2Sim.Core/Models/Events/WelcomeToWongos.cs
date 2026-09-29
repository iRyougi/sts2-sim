using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class WelcomeToWongos : EventModel
{
    private RelicModel? _featuredItem;
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: 1 } &&
        runState.Players.All(p => p.Gold >= 100);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        _featuredItem = PullShopRelic(RelicRarity.Rare);
        return [PurchaseOption("BARGAIN_BIN", 100, BuyBargain), PurchaseOption("FEATURED_ITEM", 200, BuyFeatured),
            PurchaseOption("MYSTERY_BOX", 300, BuyMystery), new("LEAVE", Leave)];
    }

    private EventOption PurchaseOption(string key, int cost, Func<Task> action) =>
        new(Owner.Gold >= cost ? key : key + "_LOCKED", Owner.Gold >= cost ? action : null);

    private RelicModel PullShopRelic(RelicRarity rarity) =>
        RelicFactory.PullNextRelicFromFront(Owner, rarity, relic => relic.IsAllowedInShops);

    private async Task BuyBargain()
    {
        await PlayerCmd.LoseGold(100, Owner, GoldLossType.Spent);
        await RelicCmd.Obtain(PullShopRelic(RelicRarity.Common), Owner);
        await AwardPoints(32);
    }

    private async Task BuyFeatured()
    {
        await PlayerCmd.LoseGold(200, Owner, GoldLossType.Spent);
        await RelicCmd.Obtain(_featuredItem!, Owner);
        await AwardPoints(16);
    }

    private async Task BuyMystery()
    {
        await PlayerCmd.LoseGold(300, Owner, GoldLossType.Spent);
        await RelicCmd.Obtain(ModelDb.Relic<WongosMysteryTicket>(), Owner);
        await AwardPoints(8);
    }

    private async Task AwardPoints(int points)
    {
        if (RunState is RunState run)
        {
            if (run.Progress.WongoPoints % 2000 + points >= 2000)
                await RelicCmd.Obtain(ModelDb.Relic<WongoCustomerAppreciationBadge>(), Owner);
            run.WongoPointsEarned = points;
        }
        Finish();
    }

    private Task Leave()
    {
        Rng.NextItem(Owner.Deck.Cards.Where(c => c.IsUpgraded))?.Downgrade();
        Finish();
        return Task.CompletedTask;
    }
}
