using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rooms;

/// <summary>Treasure room that grants rolled gold and a relic from the shared run bag.</summary>
public sealed class TreasureRoom : AbstractRoom
{
    private readonly int? _goldAmount;
    private IReadOnlyList<TreasureRoomResolution> _resolutions = Array.Empty<TreasureRoomResolution>();

    internal IReadOnlyList<TreasureRoomResolution> Resolutions => _resolutions;

    // Upstream enters the room (including hooks) before its separate reward synchronization.
    protected override bool EntryHooksBeforeRewards => true;

    public override RoomType RoomType => RoomType.Treasure;

    public override ModelId? ModelId => null;

    public TreasureRoom()
    {
    }

    /// <summary>Explicit gold override for controlled room scenarios; skips the gold roll.</summary>
    public TreasureRoom(int goldAmount)
    {
        _goldAmount = goldAmount;
    }

    internal TreasureRoom CloneForRun(Func<Player, Player> mapPlayer)
    {
        TreasureRoom clone = _goldAmount is { } gold ? new TreasureRoom(gold) : new TreasureRoom();
        clone.CopyEntryStateFrom(this);
        clone._resolutions = Array.AsReadOnly(_resolutions
            .Select(result => result with { Player = mapPlayer(result.Player) }).ToArray());
        return clone;
    }

    public override async Task EnterInternal(RunState? runState)
    {
        ArgumentNullException.ThrowIfNull(runState);
        _resolutions = Array.Empty<TreasureRoomResolution>();
        var resolutions = new List<TreasureRoomResolution>();
        foreach (Player player in runState.Players)
        {
            // 偏离 #154：地图上的宝箱节点保持不变；被抑制时仅跳过该玩家的金币与遗物发放。
            if (!Hooks.Hook.ShouldGenerateTreasure(runState, player))
            {
                resolutions.Add(new TreasureRoomResolution(player, 0, null));
                continue;
            }

            int goldBefore = player.Gold;
            int goldAmount = _goldAmount ?? (int)(player.PlayerRng.ForSemanticKey(
                    PlayerRngType.Rewards,
                    $"{runState.SemanticLocationKey}/treasure/slot=gold").NextInt(42, 53) *
                (runState.Ascension.HasLevel(AscensionLevel.Poverty) ? 0.75m : 1m));
            await PlayerCmd.GainGold(goldAmount, player);
            if (runState.CurrentMapPoint?.Quests.Any(q => q is Models.Cards.SpoilsMap) == true)
            {
                foreach (var quest in player.Deck.Cards.OfType<Models.Cards.SpoilsMap>().ToArray())
                    await quest.OnQuestComplete();
            }
            int goldGained = player.Gold - goldBefore;
            RelicRarity rarity = RelicFactory.RollRarity(runState.Rng.ForSemanticKey(
                RunRngType.TreasureRoomRelics,
                $"{runState.SemanticLocationKey}/treasure/slot=relic"));
            RelicGrabBag sharedBag = runState.SharedRelicGrabBag
                ?? throw new InvalidOperationException("Shared relic bag must be initialized before entering treasure.");
            RelicModel relic = sharedBag.PullFromFront(rarity, runState)
                ?? ModelDb.Relic<Sts2Sim.Core.Models.Relics.Circlet>();
            await RelicCmd.Obtain(relic, player);
            resolutions.Add(new TreasureRoomResolution(
                player,
                goldGained,
                relic.Id.ToString()));
        }

        _resolutions = Array.AsReadOnly(resolutions.ToArray());
    }

    public override Task Exit(RunState? runState) => Task.CompletedTask;
}
