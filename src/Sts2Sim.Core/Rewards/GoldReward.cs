using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Entities.Rngs;

namespace Sts2Sim.Core.Rewards;

/// <summary>Gold reward from the player's reward RNG stream. #74/#310/#313 resolved:
/// encounter bounds replace room defaults, then escape proportion scales and rounds both bounds.</summary>
public sealed class GoldReward : TakeableReward
{
    private const double PovertyGoldMultiplier = 0.75;
    private readonly bool _representsOmittedReward;
    private readonly int _min;
    private readonly int _max;

    public int Amount { get; private set; }

    public GoldReward(RoomType roomType, Player player)
        : this(roomType, player, 1f, null, null)
    {
    }

    public GoldReward(
        RoomType roomType, Player player, float goldProportion,
        int? minGoldReward = null, int? maxGoldReward = null) : base(player)
    {
        (double min, double max) = roomType switch
        {
            RoomType.Monster => (10, 20),
            RoomType.Elite => (35, 45),
            RoomType.Boss => (100, 100),
            _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, "Gold rewards only apply to combat rooms."),
        };

        if (player.RunState.Ascension.HasLevel(AscensionLevel.Poverty))
        {
            min *= PovertyGoldMultiplier;
            max *= PovertyGoldMultiplier;
        }

        _representsOmittedReward = goldProportion == 0f;
        _min = (int)Math.Round((minGoldReward ?? (int)min) * goldProportion);
        _max = (int)Math.Round((maxGoldReward ?? (int)max) * goldProportion);
    }

    /// <summary>Creates an already-populated fixed-amount reward. Populate still draws its equal bounds.</summary>
    public GoldReward(int amount, Player player) : base(player)
    {
        _min = amount;
        _max = amount;
        Amount = amount;
    }

    public override void Populate(IRunState runState)
        => Populate(
            runState,
            Player.PlayerRng.ForCurrentScopeOrSemanticKey(
                PlayerRngType.Rewards,
                $"{Player.CurrentSemanticLocationKey}/reward/slot=gold"));

    internal void Populate(IRunState runState, Rng rng)
    {
        // The native reward set calls Populate even on a fixed reward that was already
        // populated by its constructor. An omitted zero-proportion slot is different:
        // #310 retains its object for the simulator but upstream has no reward to populate.
        Amount = _representsOmittedReward ? 0 : rng.NextInt(_min, _max + 1);
    }

    protected override Task OnTake() => PlayerCmd.GainGold(Amount, Player);
}
