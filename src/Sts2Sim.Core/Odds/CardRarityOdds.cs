using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Odds;

public class CardRarityOdds : AbstractOdds
{
    public const float regularUncommonOdds = 0.37f;

    public const float eliteUncommonOdds = 0.4f;

    public const float bossCommonOdds = 0f;

    public const float bossUncommonOdds = 0f;

    public const float bossRareOdds = 1f;

    public const float shopUncommonOdds = 0.37f;

    private const float _baseRarityOffset = -0.05f;

    private const float _maxRarityOffset = 0.4f;

    private readonly AscensionManager _ascension;

    public float RegularCommonOdds => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.615f, 0.6f);

    public float RarityGrowth => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.005f, 0.01f);

    public float RegularRareOdds => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.0149f, 0.03f);

    public float EliteCommonOdds => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.549f, 0.5f);

    public float EliteRareOdds => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.05f, 0.1f);

    public float ShopCommonOdds => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.585f, 0.54f);

    public float ShopRareOdds => _ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.045f, 0.09f);

    public CardRarityOdds(Rng rng, AscensionManager ascension)
        : this(_baseRarityOffset, rng, ascension)
    {
    }

    public CardRarityOdds(float initialValue, Rng rng, AscensionManager ascension)
        : base(initialValue, rng)
    {
        _ascension = ascension;
    }

    public CardRarity Roll(CardRarityOddsType type) => Roll(type, _rng);

    public CardRarity Roll(CardRarityOddsType type, Rng rng)
    {
        CardRarity cardRarity = RollWithoutChangingFutureOdds(
            type,
            type == CardRarityOddsType.BossEncounter ? 0f : CurrentValue,
            rng);
        if (cardRarity == CardRarity.Rare)
        {
            CurrentValue = _baseRarityOffset;
        }
        else
        {
            CurrentValue = Math.Min(CurrentValue + RarityGrowth, _maxRarityOffset);
        }
        return cardRarity;
    }

    public CardRarity RollWithoutChangingFutureOdds(CardRarityOddsType oddsType)
    {
        return RollWithoutChangingFutureOdds(oddsType, CurrentValue);
    }

    public CardRarity RollWithoutChangingFutureOdds(CardRarityOddsType oddsType, Rng rng) =>
        RollWithoutChangingFutureOdds(oddsType, CurrentValue, rng);

    public CardRarity RollWithoutChangingFutureOdds(CardRarityOddsType type, float offset)
        => RollWithoutChangingFutureOdds(type, offset, _rng);

    public CardRarity RollWithoutChangingFutureOdds(CardRarityOddsType type, float offset, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        float num = rng.NextFloat();
        float num2 = GetBaseOdds(type, CardRarity.Rare) + offset;
        if (num < num2)
        {
            return CardRarity.Rare;
        }
        if (num < GetBaseOdds(type, CardRarity.Uncommon) + num2)
        {
            return CardRarity.Uncommon;
        }
        return CardRarity.Common;
    }

    public CardRarity RollWithBaseOdds(CardRarityOddsType type)
        => RollWithBaseOdds(type, _rng);

    public CardRarity RollWithBaseOdds(CardRarityOddsType type, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        float num = rng.NextFloat();
        if (num < GetBaseOdds(type, CardRarity.Rare))
        {
            return CardRarity.Rare;
        }
        if (num < GetBaseOdds(type, CardRarity.Rare) + GetBaseOdds(type, CardRarity.Uncommon))
        {
            return CardRarity.Uncommon;
        }
        return CardRarity.Common;
    }

    private float GetBaseOdds(CardRarityOddsType type, CardRarity rarity)
    {
        return type switch
        {
            CardRarityOddsType.EliteEncounter => rarity switch
            {
                CardRarity.Common => EliteCommonOdds,
                CardRarity.Uncommon => eliteUncommonOdds,
                CardRarity.Rare => EliteRareOdds,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
            },
            CardRarityOddsType.BossEncounter => rarity switch
            {
                CardRarity.Common => bossCommonOdds,
                CardRarity.Uncommon => bossUncommonOdds,
                CardRarity.Rare => bossRareOdds,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
            },
            CardRarityOddsType.Shop => rarity switch
            {
                CardRarity.Common => ShopCommonOdds,
                CardRarity.Uncommon => shopUncommonOdds,
                CardRarity.Rare => ShopRareOdds,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
            },
            CardRarityOddsType.RegularEncounter => rarity switch
            {
                CardRarity.Common => RegularCommonOdds,
                CardRarity.Uncommon => regularUncommonOdds,
                CardRarity.Rare => RegularRareOdds,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
            },
            CardRarityOddsType.Uniform => rarity switch
            {
                CardRarity.Common => 0.33f,
                CardRarity.Uncommon => 0.33f,
                CardRarity.Rare => 0.33f,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }
}
