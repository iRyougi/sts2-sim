using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Odds;

public class PlayerOddsSet
{
    public CardRarityOdds CardRarity { get; private init; } = null!;

    public PotionRewardOdds PotionReward { get; private init; } = null!;

    private PlayerOddsSet()
    {
    }

    public PlayerOddsSet(PlayerRngSet rng, AscensionManager ascension, IOddsHooks hooks)
    {
        CardRarity = new CardRarityOdds(rng.Rewards, ascension);
        PotionReward = new PotionRewardOdds(rng.Rewards, hooks);
    }

    public SerializablePlayerOddsSet ToSerializable()
    {
        return new SerializablePlayerOddsSet
        {
            CardRarityOddsValue = CardRarity.CurrentValue,
            PotionRewardOddsValue = PotionReward.CurrentValue,
        };
    }

    public static PlayerOddsSet FromSerializable(SerializablePlayerOddsSet save, PlayerRngSet rng, AscensionManager ascension, IOddsHooks hooks)
    {
        return new PlayerOddsSet
        {
            CardRarity = new CardRarityOdds(save.CardRarityOddsValue, rng.Rewards, ascension),
            PotionReward = new PotionRewardOdds(save.PotionRewardOddsValue, rng.Rewards, hooks),
        };
    }

    public void LoadFromSerializable(SerializablePlayerOddsSet save)
    {
        CardRarity.OverrideCurrentValue(save.CardRarityOddsValue);
        PotionReward.OverrideCurrentValue(save.PotionRewardOddsValue);
    }
}
