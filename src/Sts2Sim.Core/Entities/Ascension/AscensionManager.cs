using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Entities.Ascension;

public class AscensionManager
{
    public const int maxAscensionAllowed = 10;

    private readonly int _level;

    public AscensionManager(int level)
    {
        _level = level;
    }

    public AscensionManager(AscensionLevel level)
    {
        _level = (int)level;
    }

    public bool HasLevel(AscensionLevel level)
    {
        return _level >= (int)level;
    }

    // 游戏 AscensionHelper.GetValueIfAscension 的实例化版本（全局静态 → 注入，见偏离清单 #6）
    public int GetValueIfAscension(AscensionLevel level, int ascensionValue, int fallbackValue)
    {
        return HasLevel(level) ? ascensionValue : fallbackValue;
    }

    public float GetValueIfAscension(AscensionLevel level, float ascensionValue, float fallbackValue)
    {
        return HasLevel(level) ? ascensionValue : fallbackValue;
    }

    public decimal GetValueIfAscension(AscensionLevel level, decimal ascensionValue, decimal fallbackValue)
    {
        return HasLevel(level) ? ascensionValue : fallbackValue;
    }

    public void ApplyEffectsTo(Player player)
    {
        if (HasLevel(AscensionLevel.TightBelt))
        {
            player.SubtractFromMaxPotionCount(1);
        }

        if (HasLevel(AscensionLevel.AscendersBane))
        {
            var bane = (AscendersBane)ModelDb.Card<AscendersBane>().MutableClone();
            bane.AssignOwner(player);
            bane.FloorAddedToDeck = 1;
            player.Deck.AddInternal(bane, -1);
        }
    }
}
