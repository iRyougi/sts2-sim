using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class WongosMysteryTicket : RelicModel
{
    public const int CombatsToActivate = 5;
    public const int RelicCount = 3;

    private int _combatsFinished;
    private bool _gaveRelics;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsUsedUp => _gaveRelics;

    public int CombatsFinished => _combatsFinished;

    public int RemainingCombats => Math.Max(0, CombatsToActivate - _combatsFinished);

    public override Task AfterCombatEnd()
    {
        if (!_gaveRelics)
        {
            _combatsFinished++;
        }

        return Task.CompletedTask;
    }

    public override void ModifyRewards(
        Player player,
        List<Reward> rewards,
        RoomType roomType)
    {
        if (!ReferenceEquals(player, Owner) ||
            _gaveRelics ||
            _combatsFinished < CombatsToActivate ||
            roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
        {
            return;
        }

        for (int i = 0; i < RelicCount; i++)
        {
            var reward = new RelicReward(player);
            reward.Populate(Owner.RunState);
            rewards.Add(reward);
        }

        _gaveRelics = true;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_combatsFinished);
        builder.Append(_gaveRelics);
    }
}
