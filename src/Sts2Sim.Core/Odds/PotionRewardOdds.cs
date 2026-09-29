using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Odds;

public class PotionRewardOdds : AbstractOdds
{
    public const float targetOdds = 0.5f;

    public const float eliteBonus = 0.25f;

    private const float _basePotionRewardOdds = 0.4f;

    private readonly IOddsHooks _hooks;

    public PotionRewardOdds(Rng rng, IOddsHooks hooks)
        : this(_basePotionRewardOdds, rng, hooks)
    {
    }

    public PotionRewardOdds(float initialValue, Rng rng, IOddsHooks hooks)
        : base(initialValue, rng)
    {
        _hooks = hooks;
    }

    public bool Roll(RoomType roomType) => Roll(roomType, _rng);

    public bool Roll(RoomType roomType, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        float currentValue = CurrentValue;
        if (_hooks.ShouldForcePotionReward(roomType))
        {
            return true;
        }
        float bonus = roomType == RoomType.Elite ? eliteBonus : 0f;
        float threshold = currentValue + bonus * 0.5f;
        float roll = rng.NextFloat();
        if (roll < threshold)
        {
            CurrentValue -= 0.1f;
            return true;
        }
        CurrentValue += 0.1f;
        return false;
    }
}
