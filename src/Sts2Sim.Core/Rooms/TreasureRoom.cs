using Sts2Sim.Core.Helpers;
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

    // Native TreasureRoomRelicSynchronizer lets the player pick a chest relic or skip it (PickRelicLocally(index) /
    // SkipRelicLocally). Null = no decision source: the relic is taken, as before.
    private Func<Player, IReadOnlyList<RelicModel>, Task<int?>>? _chooseRelic;

    /// <summary>Routes the chest relic choice to a run decision source (<see cref="IRunDecisionSource.ChooseTreasureRelicAsync"/>).</summary>
    internal void ConfigureRelicChoice(Func<Player, IReadOnlyList<RelicModel>, Task<int?>> choose) =>
        _chooseRelic = choose ?? throw new ArgumentNullException(nameof(choose));

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
        if (runState.GameMode == GameMode.Blitz)
        {
            await EnterBlitzAsync(runState);
            return;
        }

        var relics = new List<(Player Player, RelicModel? Relic)>();
        foreach (Player player in runState.Players)
        {
            // 偏离 #154：地图上的宝箱节点保持不变；被抑制时仅跳过该玩家的金币与遗物发放。
            if (!Hooks.Hook.ShouldGenerateTreasure(runState, player))
            {
                relics.Add((player, null));
                continue;
            }

            RelicRarity rarity = RelicFactory.RollRarity(runState.Rng.ForSemanticKey(
                RunRngType.TreasureRoomRelics,
                $"{runState.SemanticLocationKey}/treasure/slot=relic"));
            RelicGrabBag sharedBag = runState.SharedRelicGrabBag
                ?? throw new InvalidOperationException("Shared relic bag must be initialized before entering treasure.");
            RelicModel relic = sharedBag.PullFromFront(rarity, runState)
                ?? ModelDb.Relic<Sts2Sim.Core.Models.Relics.Circlet>();
            relics.Add((player, relic));
        }

        // All relics are picked before gold and quest rewards; obtain effects still run afterward.
        var picks = new List<(Player Player, int GoldGained, RelicModel? Relic, bool Skipped)>();
        foreach ((Player player, RelicModel? relic) in relics)
        {
            if (relic is null)
            {
                picks.Add((player, 0, null, false));
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
            // Native: the chest's gold is granted before relic picking (NTreasureRoom.OpenChest); a singleplayer skip
            // (TreasureRoomRelicSynchronizer.OnPicked with a null index) returns before AwardRelics: the relic is not
            // obtained, not returned to the bag, and no further random numbers are drawn.
            int? pick = _chooseRelic is null ? 0 : await _chooseRelic(player, new[] { relic });
            if (pick is not (null or 0))
                throw new InvalidOperationException($"Treasure relic choice {pick} is outside the offered relics.");
            picks.Add((player, goldGained, relic, pick is null));
        }

        // Native TreasureRoomRelicSynchronizer.OnPicked → AwardRelics (once every vote is in): the indices nobody voted
        // for are StableShuffle'd on TreasureRoomRelics before RelicsAwarded obtains the relics. ListExtensions.
        // UnstableShuffle draws once from the parent stream even for an empty list (#1102; v0.111.0 did not draw).
        // A singleplayer skip returns before AwardRelics. Each player is offered only their own relic here (no
        // multiplayer voting), so the unvoted indices are the relics of players who skipped.
        var offered = picks.Where(p => p.Relic is not null).ToList();
        bool singleplayerSkipped = runState.Players.Count == 1 && offered.Any(p => p.Skipped);
        if (offered.Count > 0 && !singleplayerSkipped)
        {
            List<int> unvoted = Enumerable.Range(0, offered.Count).Where(index => offered[index].Skipped).ToList();
            unvoted.StableShuffle(runState.Rng.ForSemanticKey(
                RunRngType.TreasureRoomRelics,
                $"{runState.SemanticLocationKey}/treasure/award"));
        }

        var resolutions = new List<TreasureRoomResolution>();
        foreach ((Player player, int goldGained, RelicModel? relic, bool skipped) in picks)
        {
            if (relic is null || skipped)
            {
                resolutions.Add(new TreasureRoomResolution(player, goldGained, null));
                continue;
            }
            await RelicCmd.Obtain(relic, player);
            resolutions.Add(new TreasureRoomResolution(
                player,
                goldGained,
                relic.Id.ToString()));
        }

        _resolutions = Array.AsReadOnly(resolutions.ToArray());
    }

    /// <summary>
    /// v0.114.0 <c>TreasureRoomRelicSynchronizer.BeginRelicPicking</c> in Blitz: the usual chest relic (when
    /// <c>ShouldGenerateTreasure</c>) plus one extra relic per player, picked over two rounds; <c>OneOffSynchronizer</c>
    /// grants no chest gold in Blitz. Singleplayer only: a skip sets <c>_singleplayerSkipped</c> and returns before
    /// <c>AwardRelics</c>, ending the picking. Each <c>AwardRelics</c> StableShuffles the unvoted indices on
    /// TreasureRoomRelics (draws once even when empty, #1102) and obtains the pick before the next round.
    /// Multiplayer voting (buffered per-round votes, consolation prizes) is not ported and fails closed.
    /// </summary>
    private async Task EnterBlitzAsync(RunState runState)
    {
        if (runState.Players.Count > 1)
            throw new NotSupportedException("Multiplayer Blitz chest voting is unsupported.");
        const int rounds = 2;
        Player player = runState.Players[0];
        var playerRelics = new List<RelicModel>();
        if (Hooks.Hook.ShouldGenerateTreasure(runState, player))
            playerRelics.Add(PullRelic("relic"));
        playerRelics.Add(PullRelic("relic_blitz_extra"));

        int goldGained = 0;
        if (_goldAmount is { } goldOverride)
        {
            int goldBefore = player.Gold;
            // Controlled-scenario override only (native Blitz grants no chest gold); kept as in the Standard path.
            await PlayerCmd.GainGold(goldOverride, player);
            if (runState.CurrentMapPoint?.Quests.Any(q => q is Models.Cards.SpoilsMap) == true)
            {
                foreach (var quest in player.Deck.Cards.OfType<Models.Cards.SpoilsMap>().ToArray())
                    await quest.OnQuestComplete();
            }
            goldGained = player.Gold - goldBefore;
        }

        var obtained = new List<string>();
        for (int round = 0; round < rounds && playerRelics.Count > 0; round++)
        {
            int? pick = _chooseRelic is null ? 0 : await _chooseRelic(player, playerRelics.ToArray());
            if (pick is null) break;
            if (pick < 0 || pick >= playerRelics.Count)
                throw new InvalidOperationException($"Treasure relic choice {pick} is outside the offered relics.");
            List<int> unvoted = Enumerable.Range(0, playerRelics.Count).Where(index => index != pick.Value).ToList();
            unvoted.StableShuffle(runState.Rng.ForSemanticKey(
                RunRngType.TreasureRoomRelics,
                round == 0
                    ? $"{runState.SemanticLocationKey}/treasure/award"
                    : $"{runState.SemanticLocationKey}/treasure/award/round={round}"));
            RelicModel selected = playerRelics[pick.Value];
            playerRelics.RemoveAt(pick.Value);
            await RelicCmd.Obtain(selected, player);
            obtained.Add(selected.Id.ToString());
        }

        _resolutions = Array.AsReadOnly(new[]
        {
            new TreasureRoomResolution(player, goldGained, obtained.FirstOrDefault(),
                obtained.Count > 1 ? Array.AsReadOnly(obtained.Skip(1).ToArray()) : null),
        });

        RelicModel PullRelic(string slot)
        {
            RelicRarity rarity = RelicFactory.RollRarity(runState.Rng.ForSemanticKey(
                RunRngType.TreasureRoomRelics,
                $"{runState.SemanticLocationKey}/treasure/slot={slot}"));
            RelicGrabBag bag = runState.SharedRelicGrabBag
                ?? throw new InvalidOperationException("Shared relic bag must be initialized before entering treasure.");
            return bag.PullFromFront(rarity, runState) ?? ModelDb.Relic<Models.Relics.Circlet>();
        }
    }

    public override Task Exit(RunState? runState) => Task.CompletedTask;
}
