using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.Ascension;

public class AscensionManager
{
    public const int maxAscensionAllowed = 10;
    public const int maxAscensionAllowedBlitz = 5;

    private readonly int _level;
    private readonly HashSet<AscensionLevel>? _blitzAscensions;

    public GameMode GameMode { get; }

    public int Level => _level;

    public bool IsDread => GameMode == GameMode.Dread;

    public AscensionManager(int level)
        : this(level, GameMode.Standard)
    {
    }

    public AscensionManager(AscensionLevel level)
        : this((int)level, GameMode.Standard)
    {
    }

    public AscensionManager(int level, GameMode gameMode)
    {
        if (level < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Ascension level cannot be negative.");
        }

        _level = level;
        GameMode = gameMode;
        if (gameMode == GameMode.Blitz)
        {
            _blitzAscensions = new HashSet<AscensionLevel>();
            for (int i = 1; i <= level; i++)
            {
                _blitzAscensions.Add(GetLevelForBlitz(i));
            }
        }
    }

    public static int GetMaxAllowed(GameMode gameMode) => gameMode switch
    {
        GameMode.Blitz => maxAscensionAllowedBlitz,
        _ => maxAscensionAllowed,
    };

    public static int ClampToMode(GameMode gameMode, int level) =>
        Math.Clamp(level, 0, GetMaxAllowed(gameMode));

    public static AscensionLevel GetLevelForBlitz(int blitzLevel) => blitzLevel switch
    {
        0 => AscensionLevel.None,
        1 => AscensionLevel.WearyTraveler,
        2 => AscensionLevel.TightBelt,
        3 => AscensionLevel.ToughEnemies,
        4 => AscensionLevel.DeadlyEnemies,
        5 => AscensionLevel.AscendersBane,
        _ => throw new InvalidOperationException($"Invalid blitz ascension level {blitzLevel}"),
    };

    public bool HasLevel(AscensionLevel level)
    {
        if (GameMode == GameMode.Blitz)
        {
            return _blitzAscensions!.Contains(level);
        }

        return _level >= (int)level && (GameMode == GameMode.Standard || level > AscensionLevel.None);
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
